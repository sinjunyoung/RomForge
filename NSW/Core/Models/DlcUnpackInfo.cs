using NSW.Core.Services;
using System.IO;
using System.Text.Json.Serialization;

namespace NSW.Core.Models;

public class DlcUnpackInfo
{
    public ulong TitleId { get; set; }

    public string Dir { get; set; } = string.Empty;

    [JsonIgnore]
    public Func<Stream>? RomfsOpener { get; set; }

    public VirtualRomfs? Virtual { get; set; }

    public bool SourceCompressed { get; set; }
}