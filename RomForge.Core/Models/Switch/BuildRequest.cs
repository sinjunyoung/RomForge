using Common.Models;
using NSW.Core.Models;

namespace RomForge.Core.Models.Switch;

public sealed record BuildRequest(string BaseFilePath, string UpdateFilePath, IReadOnlyList<string> DlcFilePaths, string OutputDir)
{
    public bool UseCompression { get; set; } = true;

    public int CompressionLevel { get; set; } = 18;

    public SwitchCompressionMode CompressionMode { get; set; } = SwitchCompressionMode.Block;

    public IReadOnlyList<string>? AllSourcePaths { get; set; }

    public HashSet<string>? AllowedNcaIds { get; set; }

    public string? TargetBaseTitleId { get; set; }

    public string? TargetBaseTitleName { get; set; }

    public MetadataResult? ResolvedMeta { get; set; }
}