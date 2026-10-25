using System.IO.Compression;

namespace Vita.Core.Services;

public sealed class ZipSourceAccessor : ISourceAccessor
{
    private readonly FileStream _stream;
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    public ZipSourceAccessor(string zipPath)
    {
        _stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _zip = new ZipArchive(_stream, ZipArchiveMode.Read);
        _entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _zip.Entries)
            _entries.TryAdd(Normalize(entry.FullName), entry);
    }

    private static string Normalize(string path) => PatchShared.NormalizeZipPath(path);

    public bool DirectoryExists(string relativePath)
    {
        string prefix = Normalize(relativePath);

        if (prefix.Length == 0)
            return _entries.Count > 0;

        prefix += "/";

        return _entries.Keys.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> EnumerateDirectoryNames(string relativePath)
    {
        string prefix = Normalize(relativePath);

        if (prefix.Length > 0)
            prefix += "/";

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _entries.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string rest = key[prefix.Length..];
            int slash = rest.IndexOf('/');

            if (slash <= 0)
                continue;

            names.Add(rest[..slash]);
        }

        return names;
    }

    public IEnumerable<string> EnumerateAllFiles()
    {
        return _entries.Values
            .Where(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'))
            .Select(e => Normalize(e.FullName));
    }

    public bool FileExists(string relativePath) => _entries.ContainsKey(Normalize(relativePath));

    public byte[] ReadAllBytes(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        var data = new byte[entry.Length];
        using var s = entry.Open();

        s.ReadExactly(data);

        return data;
    }

    public long GetFileSize(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        return entry.Length;
    }

    public void Dispose()
    {
        _zip.Dispose();
        _stream.Dispose();
    }
}