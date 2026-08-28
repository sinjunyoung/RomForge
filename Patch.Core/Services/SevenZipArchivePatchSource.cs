using SevenZip;

namespace Patch.Core.Services;

public sealed class SevenZipArchivePatchSource : IArchivePatchSource
{
    private readonly string _archivePath;
    private readonly string? _password;
    private readonly string _tempDir;
    private readonly Dictionary<string, ArchiveFileInfo> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _extractedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _extractLock = new();
    private bool _tempDirCreated;

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
        }
        catch (Exception)
        {
            throw new ArchivePasswordRequiredException(path);
        }

        if (_byKey.Count == 0)
            throw new InvalidOperationException("압축 파일에 항목이 없습니다.");

        EntryPaths = [.. _byKey.Keys];
    }

    private string ExtractToDisk(string key, ArchiveFileInfo info)
    {
        lock (_extractLock)
        {
            if (_extractedPaths.TryGetValue(key, out var cached))
                return cached;

            if (!_tempDirCreated)
            {
                Directory.CreateDirectory(_tempDir);
                _tempDirCreated = true;
            }

            var destPath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));

            using var extractor = string.IsNullOrEmpty(_password) ? new SevenZipExtractor(_archivePath) : new SevenZipExtractor(_archivePath, _password);

            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                extractor.ExtractFile(info.Index, fileStream);

            _extractedPaths[key] = destPath;
            return destPath;
        }
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
            var diskPath = owner.ExtractToDisk(fullPath, info);
            return new FileStream(diskPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}