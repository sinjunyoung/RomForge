using SevenZip;

namespace Patch.Core.Services;

public sealed class SevenZipArchivePatchSource : IArchivePatchSource
{
    private readonly string _archivePath;
    private readonly string? _password;
    private readonly string _tempDir;
    private readonly Dictionary<string, ArchiveFileInfo> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _extractedPaths = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> EntryPaths { get; }

    public bool SupportsCheapRepeatedOpen => true;

    public SevenZipArchivePatchSource(string path, string? password = null)
    {
        NativeSevenZip.EnsureInitialized();

        _archivePath = path;
        _password = password;
        _tempDir = Path.Combine(Path.GetDirectoryName(path)!, "romforge_7z_" + Guid.NewGuid().ToString("N"));

        try
        {
            using var extractor = string.IsNullOrEmpty(password) ? new SevenZipExtractor(path) : new SevenZipExtractor(path, password);

            foreach (var info in extractor.ArchiveFileData)
            {
                if (info.IsDirectory)
                    continue;

                _byKey[info.FileName.Replace('\\', '/')] = info;
            }

            if (_byKey.Count == 0)
                throw new InvalidOperationException("압축 파일에 항목이 없습니다.");

            Directory.CreateDirectory(_tempDir);

            int[] allIndexes = [.. _byKey.Values.Select(info => (int)info.Index)];
            extractor.ExtractFiles(_tempDir, allIndexes);

            foreach (var kvp in _byKey)
            {
                string relativePath = kvp.Key.Replace('/', Path.DirectorySeparatorChar);
                string destPath = Path.Combine(_tempDir, relativePath);

                if (File.Exists(destPath))
                    _extractedPaths[kvp.Key] = destPath;
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArchivePasswordRequiredException)
        {
            throw new ArchivePasswordRequiredException(path);
        }

        EntryPaths = [.. _byKey.Keys];
    }

    private string ExtractToDisk(string key)
    {
        if (_extractedPaths.TryGetValue(key, out var cached))
            return cached;

        throw new FileNotFoundException($"압축 파일 내에서 경로를 찾을 수 없습니다: {key}");
    }

    public IArchivePatchEntry? FindEntry(string path)
    {
        if (!_byKey.TryGetValue(path, out var info))
            return null;

        return new Entry(this, info, path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    private sealed class Entry(SevenZipArchivePatchSource owner, ArchiveFileInfo info, string fullPath) : IArchivePatchEntry
    {
        public string FullPath => fullPath;

        public long Length => (long)info.Size;

        public Stream Open()
        {
            var diskPath = owner.ExtractToDisk(fullPath);

            return new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}