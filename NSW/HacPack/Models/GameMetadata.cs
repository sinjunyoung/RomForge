using LibHac.Ncm;

namespace NSW.HacPack.Models;

public class GameMetadata
{
    public List<LanguageInfo> Languages { get; set; } = [];

    public ContentMetaType Type { get; set; }

    public uint Version { get; set; }

    public List<byte> Indices { get; set; } = [];
}