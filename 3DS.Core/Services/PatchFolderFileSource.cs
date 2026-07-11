using _3DS.Core.Interfaces;
using Patch.Core;

namespace _3DS.Core.Services;

public class PatchFolderFileSource(string patchFolder) : IRomFsFileSource
{
    private static readonly string[] PatchExtensions = [".xdelta", ".ips", ".bps", ".ups", ".ppf", ".aps"];

    private Dictionary<string, string>? _patchIndex;

    private readonly Dictionary<string, byte[]> _resultCache = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, CancellationToken ct = default)
    {
        string relative = fullPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string localPath = Path.Combine(patchFolder, relative);

        if (File.Exists(localPath))
            return File.OpenRead(localPath);

        if (_resultCache.TryGetValue(fullPath, out byte[]? cachedResult))
            return new MemoryStream(cachedResult);

        string targetFileName = Path.GetFileName(relative);
        var index = GetOrBuildPatchIndex();

        foreach (string ext in PatchExtensions)
        {
            string patchKey = targetFileName + ext;

            if (!index.TryGetValue(patchKey, out string? patchFilePath))
                continue;

            if (getOriginal == null)
                throw new InvalidOperationException($"패치 파일을 적용하려면 원본 데이터가 필요하지만 제공되지 않았습니다: {patchKey}");

            var originalStream = await getOriginal(ct);

            if (originalStream == null)
                throw new FileNotFoundException($"원본 파일을 찾을 수 없어 패치를 적용할 수 없습니다: {relative}");

            byte[] originalData;

            await using (originalStream)
            {
                using var ms = new MemoryStream();

                await originalStream.CopyToAsync(ms, ct);
                originalData = ms.ToArray();
            }

            byte[] patchData = await File.ReadAllBytesAsync(patchFilePath, ct);
            byte[] patchedData = await UniversalPatcher.ApplyPatchAsync(originalData, patchData, null, ct);

            _resultCache[fullPath] = patchedData;

            return new MemoryStream(patchedData);
        }

        return null;
    }

    private Dictionary<string, string> GetOrBuildPatchIndex()
    {
        if (_patchIndex != null)
            return _patchIndex;

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(patchFolder))
        {
            foreach (string path in Directory.EnumerateFiles(patchFolder, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(path);
                bool isPatchFile = false;

                foreach (string ext in PatchExtensions)
                {
                    if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        isPatchFile = true;
                        break;
                    }
                }

                if (isPatchFile)
                    index.TryAdd(name, path);
            }
        }

        _patchIndex = index;

        return _patchIndex;
    }
}