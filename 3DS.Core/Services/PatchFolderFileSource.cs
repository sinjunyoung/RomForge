using _3DS.Core.Interfaces;
using Common;
using Patch.Core;
using Patch.Core.Services;

namespace _3DS.Core.Services;

public class PatchFolderFileSource : IRomFsFileSource
{
    private static readonly string[] PatchExtensions = [".xdelta", ".ips", ".bps", ".ups", ".ppf", ".aps"];

    private readonly string? _diskFolder;
    private readonly IArchivePatchSource? _archive;
    private readonly string _archivePrefix;

    private Dictionary<string, PatchFileRef>? _patchIndex;
    private readonly Dictionary<string, byte[]> _resultCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _directCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedPaths = new(StringComparer.OrdinalIgnoreCase);

    public int AppliedCount => _appliedPaths.Count;

    private PatchFolderFileSource(string? diskFolder, IArchivePatchSource? archive, string archivePrefix)
    {
        _diskFolder = diskFolder;
        _archive = archive;
        _archivePrefix = archivePrefix;
    }

    public static PatchFolderFileSource ForFolder(string patchFolder) => new(patchFolder, null, string.Empty);

    public static PatchFolderFileSource ForArchive(IArchivePatchSource archive, string prefix) => new(null, archive, prefix);

    public async ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        string relative = fullPath.TrimStart('/').Replace('\\', '/');
        Stream? direct = OpenDirect(relative);

        if (direct != null)
        {
            if (_appliedPaths.Add(relative))
                log?.Invoke($"RomFS 교체: {relative}", LogLevel.Info);

            return direct;
        }

        if (_resultCache.TryGetValue(fullPath, out byte[]? cachedResult))
            return new MemoryStream(cachedResult);

        string targetFileName = Path.GetFileName(relative);
        var index = GetOrBuildPatchIndex();

        foreach (string ext in PatchExtensions)
        {
            string patchKey = targetFileName + ext;

            if (!index.TryGetValue(patchKey, out var patchRef))
                continue;

            if (getOriginal == null)
                throw new InvalidOperationException($"패치 파일을 적용하려면 원본 데이터가 필요하지만 제공되지 않았습니다: {relative}");

            var originalStream = await getOriginal(ct) ?? throw new FileNotFoundException($"원본 파일을 찾을 수 없어 패치를 적용할 수 없습니다: {relative}");
            byte[] originalData;

            await using (originalStream)
            {
                using var ms = new MemoryStream();

                await originalStream.CopyToAsync(ms, ct);
                originalData = ms.ToArray();
            }

            byte[] patchData = await patchRef.ReadSmallFileBytesAsync(ct);
            byte[] patchedData = await UniversalPatcher.ApplyPatchAsync(originalData, patchData, null, ct);

            _appliedPaths.Add(relative);
            _resultCache[fullPath] = patchedData;

            log?.Invoke($"RomFS {ext} 패치 적용: {relative} ({originalData.Length:N0} → {patchedData.Length:N0} bytes)", LogLevel.Ok);

            return new MemoryStream(patchedData);
        }

        return null;
    }

    private Stream? OpenDirect(string relative)
    {
        if (_diskFolder != null)
        {
            string localPath = Path.Combine(_diskFolder, relative.Replace('/', Path.DirectorySeparatorChar));

            return File.Exists(localPath) ? File.OpenRead(localPath) : null;
        }

        string archiveKey = _archivePrefix + relative;
        var entry = _archive!.FindEntry(archiveKey);

        if (entry == null)
            return null;

        if (_archive.SupportsCheapRepeatedOpen)
            return entry.Open();

        if (!_directCache.TryGetValue(relative, out byte[]? cached))
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();

            s.CopyTo(ms);
            cached = ms.ToArray();
            _directCache[relative] = cached;
        }

        return new MemoryStream(cached);
    }

    private Dictionary<string, PatchFileRef> GetOrBuildPatchIndex()
    {
        if (_patchIndex != null)
            return _patchIndex;

        var index = new Dictionary<string, PatchFileRef>(StringComparer.OrdinalIgnoreCase);

        if (_diskFolder != null)
        {
            if (Directory.Exists(_diskFolder))
            {
                foreach (string path in Directory.EnumerateFiles(_diskFolder, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(path);

                    if (PatchExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                        index.TryAdd(name, PatchFileRef.FromDisk(path));
                }
            }
        }
        else
        {
            foreach (string key in _archive!.EntryPaths)
            {
                if (!key.StartsWith(_archivePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = key.Contains('/') ? key[(key.LastIndexOf('/') + 1)..] : key;

                if (!PatchExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var entry = _archive.FindEntry(key);

                if (entry != null)
                    index.TryAdd(name, PatchFileRef.FromArchiveEntry(entry));
            }
        }

        _patchIndex = index;

        return _patchIndex;
    }
}