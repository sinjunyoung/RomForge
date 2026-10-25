namespace CHD.Core.Models;

public class ChdInfo
{
    public string FileName { get; set; }

    public uint Version { get; set; }

    public ulong LogicalBytes { get; set; }

    public uint HunkBytes { get; set; }

    public uint TotalHunks { get; set; }

    public string[] CompressionMethods { get; set; }

    public ChdSourceType SourceType { get; set; }

    public string Sha1 { get; set; }

    public string ParentSha1 { get; set; }

    public string RawSha1 { get; set; }

    public bool HasParent => !string.IsNullOrEmpty(ParentSha1) && ParentSha1 != new string('0', 40);

    public int TrackCount { get; set; }

    public TrackInfo[] Tracks { get; set; }

    public string GetCompressionInfo()
    {
        if (CompressionMethods == null || CompressionMethods.Length == 0)
            return "None";

        return string.Join(", ", CompressionMethods);
    }
}