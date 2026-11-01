using Common;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using NSW.HacPack.Models;
using NSW.HacPack.Services;
using Patch.Core.Models;
using Patch.Core.Services;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public sealed class VirtualRomfs : IPatchTarget, IDisposable
{
    private sealed class Entry(long size, Func<Stream> open)
    {
        public long Size { get; set; } = size;

        public Func<Stream> Open { get; set; } = open;

        public string? TempPath { get; set; }
    }

    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _files = new(StringComparer.Ordinal);
    private readonly string _tempDirectory;
    private readonly IDisposable? _owner;
    private readonly Lock _lock = new();

    public VirtualRomfs(IEnumerable<string> directories, IEnumerable<(string Path, long Size, Func<Stream> Open)> files, string tempDirectory, IDisposable? owner = null)
    {
        _tempDirectory = tempDirectory;
        _owner = owner;

        foreach (var directory in directories)
            _directories.Add(directory);

        foreach (var (path, size, open) in files)
            _files[path] = new Entry(size, open);
    }

    public int FileCount => _files.Count;

    public static VirtualRomfs FromFileSystem(IFileSystem fileSystem, string tempDirectory)
    {
        var directories = new List<string>();
        var files = new List<(string Path, long Size, Func<Stream> Open)>();

        foreach (var entry in fileSystem.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
        {
            string relative = entry.FullPath.TrimStart('/');

            if (entry.Type == DirectoryEntryType.Directory)
            {
                directories.Add(relative);

                continue;
            }

            string fullPath = entry.FullPath;

            files.Add((relative, entry.Size, () => OpenFile(fileSystem, fullPath)));
        }

        return new VirtualRomfs(directories, files, tempDirectory, fileSystem);
    }

    public Stream OpenImage() => RomfsBuilder.OpenImage(BuildTree());

    public IEnumerable<string> EnumerateRelativeFiles()
    {
        lock (_lock)
            return [.. _files.Keys];
    }

    public void Overwrite(string relativePath, PatchFileRef file)
    {
        lock (_lock)
        {
            if (_files.TryGetValue(relativePath, out var existing))
                DeleteTemp(existing);

            _files[relativePath] = new Entry(file.Length, file.OpenRead);
        }
    }

    public async Task<bool> PatchAsync(string relativePath, PlannedPatch patch, string displayPath, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        Entry entry;
        string tempPath;
        bool createdTemp = false;

        lock (_lock)
        {
            if (!_files.TryGetValue(relativePath, out entry!))
                return false;

            tempPath = entry.TempPath ?? string.Empty;
        }

        if (tempPath.Length == 0)
        {
            Directory.CreateDirectory(_tempDirectory);

            tempPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(relativePath));

            using (var source = entry.Open())
            using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                source.CopyTo(destination);

            createdTemp = true;
        }

        bool ok = await PatchDiskApplier.ApplyAsync(tempPath, patch, displayPath, progress, log, ct);

        lock (_lock)
        {
            if (ok)
            {
                string finalPath = tempPath;

                entry.Size = new FileInfo(finalPath).Length;
                entry.Open = () => new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
                entry.TempPath = finalPath;
            }
            else if (createdTemp)
            {
                TryDelete(tempPath);
            }
        }

        return ok;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var entry in _files.Values)
                DeleteTemp(entry);
        }

        _owner?.Dispose();
    }

    private RomfsNode BuildTree()
    {
        var root = new RomfsNode { Name = string.Empty, IsDirectory = true };
        var nodes = new Dictionary<string, RomfsNode>(StringComparer.Ordinal) { [string.Empty] = root };

        RomfsNode GetDirectory(string path)
        {
            if (nodes.TryGetValue(path, out var existing))
                return existing;

            int slash = path.LastIndexOf('/');
            var parent = GetDirectory(slash < 0 ? string.Empty : path[..slash]);
            var node = new RomfsNode { Name = path[(slash + 1)..], IsDirectory = true };

            parent.Children.Add(node);
            nodes[path] = node;

            return node;
        }

        lock (_lock)
        {
            foreach (var directory in _directories)
            {
                if (directory.Length > 0)
                    GetDirectory(directory);
            }

            foreach (var (path, entry) in _files)
            {
                int slash = path.LastIndexOf('/');
                var parent = GetDirectory(slash < 0 ? string.Empty : path[..slash]);

                parent.Children.Add(new RomfsNode { Name = path[(slash + 1)..], Size = entry.Size, Open = entry.Open });
            }
        }

        return root;
    }

    private static Stream OpenFile(IFileSystem fileSystem, string fullPath)
    {
        var file = new UniqueRef<IFile>();

        fileSystem.OpenFile(ref file.Ref, fullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

        return file.Release().AsStream();
    }

    private static void DeleteTemp(Entry entry)
    {
        if (entry.TempPath == null)
            return;

        TryDelete(entry.TempPath);

        entry.TempPath = null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"임시 파일 삭제 실패: {ex.Message}");
        }
    }
}