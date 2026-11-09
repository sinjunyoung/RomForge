using Common.Models;
using NSW.HacPack.Models;
using Patch.Core.Services;
using System.IO;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.Core.Models;

public sealed record BuildRequest(string BaseFilePath, string UpdateFilePath, IReadOnlyList<string> DlcFilePaths, string PatchDir, string OutputDir)
{
    public Language Language;

    public bool HasPatch => !string.IsNullOrEmpty(PatchDir) && (Directory.Exists(PatchDir) || ArchivePatchSourceFactory.IsArchivePath(PatchDir));

    public string? PatchPassword { get; init; }

    public IReadOnlyDictionary<string, string> DlcPatchDirs { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> DlcPatchPasswords { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public GameMetadata? UserMetadata { get; set; }

    public uint? OverrideSdkVersion { get; set; }

    public byte? OverrideKeyGeneration { get; set; }

    public ulong? OverrideTitleId { get; set; }

    public byte? TargetIdOffset { get; set; }

    public bool? CompressRomfs { get; init; }

    public bool StreamRomfs { get; init; }

    public Func<UnpackResult, GameMetadata?>? OnUnpacked { get; init; }

    public bool OutputNsz { get; init; }

    public int CompressLevel { get; init; } = 18;

    public SwitchCompressionMode CompressionMode { get; init; } = SwitchCompressionMode.Blockless;

    public string? OutputFileName { get; init; }

    public string? OutputSuffix { get; init; }

    public IReadOnlyDictionary<string, bool> DlcCompressRomfs { get; init; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, HashSet<string>>? DlcTitleIdsByFile { get; init; }
}