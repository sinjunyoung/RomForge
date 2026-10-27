using System.IO;
using System.Text.Json.Serialization;

namespace NSW.Core.Models;

public class UnpackResult
{
    public Dictionary<byte, string> ExefsDirs { get; set; } = [];

    public Dictionary<byte, string> RomfsDirs { get; set; } = [];

    public Dictionary<byte, string> LogoDirs { get; set; } = [];

    public Dictionary<byte, string> ControlDirs { get; set; } = [];

    public Dictionary<byte, string> HtmlDocDirs { get; set; } = [];

    public Dictionary<byte, string> LegalDirs { get; set; } = [];

    public List<DlcUnpackInfo> Dlcs { get; set; } = [];

    public HashSet<byte> CompressedRomfs { get; set; } = [];

    [JsonIgnore]
    public Dictionary<byte, Func<Stream>> RomfsImageOpeners { get; } = [];

    [JsonIgnore]
    public List<IDisposable> SourceResources { get; } = [];

    public void ReleaseSources()
    {
        foreach (var resource in SourceResources)
            resource.Dispose();

        SourceResources.Clear();
        RomfsImageOpeners.Clear();
    }

    [JsonIgnore]
    public int DlcCount => Dlcs.Count;

    public ulong TitleId { get; set; }

    public uint BaseSdkVersion { get; set; }

    public byte BaseKeyGeneration { get; set; }

    public string KrTitle { get; set; } = string.Empty;

    public string EnTitle { get; set; } = string.Empty;

    public uint GameVersion { get; set; }

    public string DisplayVersion { get; set; } = string.Empty;

    public uint NintendoVersion { get; set; }

    public bool HasUpdate { get; set; }

    [JsonIgnore]
    public string TitleIdStr => $"{TitleId:x16}";
}