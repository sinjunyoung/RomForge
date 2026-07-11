namespace WiiU.Core.Services;

public sealed class WudTitleSource : ITitleSource
{
    private readonly WudReader _wud;
    private readonly WuDiscReader _disc;
    private readonly Dictionary<string, int> _pathToEntry = new(StringComparer.Ordinal);

    public string TitleIdHex { get; }

    public int TitleVersion { get; }

    public WudTitleSource(WudReader wud, WuDiscReader disc)
    {
        _wud = wud;
        _disc = disc;

        foreach (var (path, idx) in WuDiscReader.EnumerateFiles(disc.GmVolume))
            _pathToEntry[path] = idx;

        string tmdPath = $"{disc.GmPartitionIndex:x2}/title.tmd";
        byte[]? tmdData = disc.ExtractFile(disc.SiVolume, tmdPath);

        if (tmdData is not null)
        {
            var tmd = TitleMetadata.Parse(tmdData);
            TitleIdHex = tmd.TitleIdHex;
            TitleVersion = tmd.TitleVersion;
        }
        else
        {
            TitleIdHex = disc.GmPartitionName.Length >= 18 ? disc.GmPartitionName[2..18].ToLowerInvariant() : "0000000000000000";
            TitleVersion = 0;
        }
    }

    public IEnumerable<string> EnumerateFiles() => _pathToEntry.Keys;

    public Stream OpenRead(string path)
    {
        if (!_pathToEntry.TryGetValue(path, out int idx))
            throw new FileNotFoundException($"\"{path}\" was not found in this title.", path);

        return _disc.OpenFileStream(_disc.GmVolume, idx);
    }

    public long GetFileSize(string path)
        => _pathToEntry.TryGetValue(path, out int idx) ? _disc.GmVolume.Entries[idx].FileSize : 0;

    public void Dispose() => _wud.Dispose();
}