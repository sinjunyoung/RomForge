using LibHac.Ncm;

namespace NSW.Core.Models;

public record MetadataResult(
    string TitleId = "", uint TitleVersion = 0, string DisplayVersion = "1.0.0", string KrTitle = "", string EnTitle = "", int DlcCount = 0, ContentMetaType Type = ContentMetaType.Application,
    string FileName = "", string SourcePath = "", IReadOnlyList<string>? ContentNcaIds = null, IReadOnlyList<byte>? ProgramIdOffsets = null)
{
    public string GetEffectiveDisplayVersion() => string.IsNullOrWhiteSpace(DisplayVersion) || DisplayVersion == "0" ? TitleVersion.ToString() : DisplayVersion;

    public string GetTypeTag() => MetadataReader.GetContentMetaTypeTag(Type);

    public byte[]? IconData { get; init; }

    public string KoreanTitle { get; init; } = string.Empty;

    public string DefaultTitle { get; init; } = string.Empty;

    public long Size { get; init; }

    public bool HasCompressedRomfs { get; init; }
}