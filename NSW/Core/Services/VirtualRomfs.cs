using Common;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using NSW.HacPack;
using NSW.HacPack.Models;
using NSW.HacPack.Services;
using Patch.Core.Models;
using Patch.Core.Services;
using System.IO;

namespace NSW.Core.Services;

public sealed class VirtualRomfs : IPatchTarget, IFileStore, IDisposable
{
    private sealed class Entry(long size, Func<Stream> open)
    {
        public long Size { get; set; } = size;

        public Func<Stream> Open { get; set; } = open;

    }

    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _files = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _owners = [];
    private readonly Lock _lock = new();

    public int FileCount => _files.Count;

    public VirtualRomfs(IEnumerable<string> directories, IEnumerable<(string Path, long Size, Func<Stream> Open)> files, IDisposable? owner = null)
    {
        if (owner != null)
            _owners.Add(owner);

        foreach (var directory in directories)
            _directories.Add(directory);

        foreach (var (path, size, open) in files)
            _files[path] = new Entry(size, open);
    }

    public static VirtualRomfs FromFileSystem(IFileSystem fileSystem)
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

        return new VirtualRomfs(directories, files, fileSystem);
    }

    public void MergeFileSystem(IFileSystem fileSystem)
    {
        lock (_lock)
        {
            _owners.Add(fileSystem);

            foreach (var entry in fileSystem.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
            {
                string relative = entry.FullPath.TrimStart('/');

                if (entry.Type == DirectoryEntryType.Directory)
                {
                    _directories.Add(relative);
                    continue;
                }

                string fullPath = entry.FullPath;

                _files[relative] = new Entry(entry.Size, () => OpenFile(fileSystem, fullPath));
            }
        }
    }

    public Stream OpenImage() => RomfsBuilder.OpenImage(BuildTree());

    public bool Exists(string relativePath)
    {
        lock (_lock)
            return _files.ContainsKey(relativePath);
    }

    public Stream OpenRead(string relativePath)
    {
        Func<Stream> open;

        lock (_lock)
            open = _files.TryGetValue(relativePath, out var entry) ? entry.Open : throw new FileNotFoundException(relativePath);

        return open();
    }

    public byte[] ReadAllBytes(string relativePath)
    {
        using var stream = OpenRead(relativePath);
        using var memory = new MemoryStream();

        stream.CopyTo(memory);

        return memory.ToArray();
    }

    public void WriteAllBytes(string relativePath, byte[] data)
    {
        lock (_lock)
            _files[relativePath] = new Entry(data.Length, () => new MemoryStream(data, false));
    }

    public List<(string Name, byte[] Data)> ReadTopLevelFiles()
    {
        List<string> names;

        lock (_lock)
            names = [.. _files.Keys.Where(k => !k.Contains('/'))];

        return [.. names.Select(n => (n, ReadAllBytes(n)))];
    }

    public IEnumerable<string> EnumerateRelativeFiles()
    {
        lock (_lock)
            return [.. _files.Keys];
    }

    public void Overwrite(string relativePath, PatchFileRef file)
    {
        lock (_lock)
            _files[relativePath] = new Entry(file.Length, file.OpenRead);
    }

    public async Task<bool> PatchAsync(string relativePath, PlannedPatch patch, string displayPath, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        Func<Stream> openSource;
        long sourceLength;

        lock (_lock)
        {
            if (!_files.TryGetValue(relativePath, out var entry))
                return false;

            openSource = entry.Open;
            sourceLength = entry.Size;
        }

        var patched = await PatchStreamApplier.ApplyAsync(openSource, sourceLength, patch, displayPath, progress, log, ct);

        if (patched == null)
            return false;

        lock (_lock)
        {
            if (_files.TryGetValue(relativePath, out var current))
            {
                current.Size = patched.Length;
                current.Open = patched.Open;
            }
        }

        return true;
    }

    public void Dispose()
    {
        for (int i = _owners.Count - 1; i >= 0; i--)
            _owners[i].Dispose();
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
}