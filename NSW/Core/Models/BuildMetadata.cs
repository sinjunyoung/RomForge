namespace NSW.Core.Models;

public class BuildMetadata
{
    public ulong TitleId { get; set; }

    public uint SdkVersion { get; set; }

    public byte KeyGeneration { get; set; }

    public string DisplayVersion { get; set; }

    public string KrTitle { get; set; }

    public string EnTitle { get; set; }
}