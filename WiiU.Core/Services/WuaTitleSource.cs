using System.Text.RegularExpressions;

namespace WiiU.Core.Services;

public sealed class WuaTitleSource : ITitleSource
{
    private readonly WuaReader _reader;
    private readonly string _rootPrefix;

    public string TitleIdHex { get; } = "0000000000000000";

    public int TitleVersion { get; }

    public WuaTitleSource(WuaReader reader)
    {
        _reader = reader;
        _rootPrefix = string.Empty;

        if (reader.GetDirEntryCount(WuaReader.RootNode) >= 1 &&
            reader.GetDirEntry(WuaReader.RootNode, 0, out var entry) && entry.IsDirectory)
        {
            _rootPrefix = entry.Name;

            var match = Regex.Match(entry.Name, @"^([0-9a-fA-F]{16})_v(\d+)$");

            if (match.Success)
            {
                TitleIdHex = match.Groups[1].Value.ToLowerInvariant();
                TitleVersion = int.Parse(match.Groups[2].Value);
            }
        }
    }

    public IEnumerable<string> EnumerateFiles()
    {
        string prefix = _rootPrefix.Length == 0 ? "" : _rootPrefix + "/";

        foreach (var (path, _) in _reader.EnumerateFiles())
            yield return path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path;
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

    public void Dispose() => _reader.Dispose();
}