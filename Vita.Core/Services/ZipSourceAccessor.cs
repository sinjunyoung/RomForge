using System.IO.Compression;

namespace Vita.Core.Services;

public sealed class ZipSourceAccessor : IVitaSourceAccessor
{
    private readonly FileStream _stream;
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;

    public ZipSourceAccessor(string zipPath)
    {
        _stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _zip = new ZipArchive(_stream, ZipArchiveMode.Read);
        _entries = _zip.Entries.ToDictionary(e => Normalize(e.FullName), e => e, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

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

    public IEnumerable<string> EnumerateAllFiles() => _entries.Keys;

    public bool FileExists(string relativePath) => _entries.ContainsKey(Normalize(relativePath));

    public byte[] ReadAllBytes(string relativePath)
    {
        if (!_entries.TryGetValue(Normalize(relativePath), out var entry))
            throw new FileNotFoundException(relativePath);

        using var s = entry.Open();
        using var ms = new MemoryStream();

        s.CopyTo(ms);

        return ms.ToArray();
    }

    public void Dispose()
    {
        _zip.Dispose();
        _stream.Dispose();
    }
}