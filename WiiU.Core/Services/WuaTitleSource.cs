using System.Text.RegularExpressions;

namespace WiiU.Core.Services;

public sealed class WuaTitleSource : ITitleSource
{
    private readonly WuaReader _reader;
    private readonly RefCountedReader? _shared;
    private readonly string _rootPrefix;

    public string TitleIdHex { get; } = "0000000000000000";

    public int TitleVersion { get; }

    public WuaTitleSource(WuaReader reader) : this(reader, null, DetectFirstRootFolder(reader))
    {
    }

    private WuaTitleSource(WuaReader reader, RefCountedReader? shared, string rootPrefix)
    {
        _reader = reader;
        _shared = shared;
        _rootPrefix = rootPrefix;

        var match = Regex.Match(rootPrefix, @"^([0-9a-fA-F]{16})_v(\d+)$");
        if (match.Success)
        {
            TitleIdHex = match.Groups[1].Value.ToLowerInvariant();
            TitleVersion = int.Parse(match.Groups[2].Value);
        }
    }

    public static IReadOnlyList<WuaTitleSource> OpenAllTitles(WuaReader reader)
    {
        var dirNames = new List<string>();
        uint count = reader.GetDirEntryCount(WuaReader.RootNode);

        for (uint i = 0; i < count; i++)
        {
            if (reader.GetDirEntry(WuaReader.RootNode, i, out var entry) && entry.IsDirectory)
                dirNames.Add(entry.Name);
        }

        if (dirNames.Count == 0) 
            dirNames.Add(string.Empty);

        var shared = new RefCountedReader(reader, dirNames.Count);

        return [.. dirNames.Select(name => new WuaTitleSource(reader, shared, name))];
    }

    private static string DetectFirstRootFolder(WuaReader reader)
    {
        if (reader.GetDirEntryCount(WuaReader.RootNode) >= 1 && reader.GetDirEntry(WuaReader.RootNode, 0, out var entry) && entry.IsDirectory)
            return entry.Name;

        return string.Empty;
    }

    public IEnumerable<string> EnumerateFiles()
    {
        string prefix = _rootPrefix.Length == 0 ? "" : _rootPrefix + "/";

        foreach (var (path, _) in _reader.EnumerateFiles())
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) 
                continue;

            yield return prefix.Length == 0 ? path : path[prefix.Length..];
        }
    }

    private string ToFullPath(string relativePath) => _rootPrefix.Length == 0 ? relativePath : $"{_rootPrefix}/{relativePath}";

    public Stream OpenRead(string path)
    {
        uint node = _reader.LookUp(ToFullPath(path));

        if (node == WuaReader.InvalidNodeHandle)
            throw new FileNotFoundException($"\"{path}\" was not found in this .wua.", path);

        return _reader.OpenFileStream(node);
    }

    public long GetFileSize(string path)
    {
        uint node = _reader.LookUp(ToFullPath(path));

        return node == WuaReader.InvalidNodeHandle ? 0 : (long)_reader.GetFileSize(node);
    }

    public void Dispose()
    {
        if (_shared is not null) 
            _shared.Release();
        else
            _reader.Dispose();
    }
}