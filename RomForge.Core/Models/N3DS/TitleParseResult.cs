using N3DS.Core.Enums;
using N3DS.Core.Models;

namespace RomForge.Core.Models.N3DS;

public class TitleParseResult
{
    public required InstalledTitle Title { get; init; }

    public required string FilePath { get; init; }

    public string? ProductCode { get; init; }

    public string? ShortDescription { get; init; }

    public string? Publisher { get; init; }

    public bool Crypto { get; init; }

    public byte[]? IconPixels { get; init; }

    public IReadOnlyList<LocaleLanguage> AvailableLanguages { get; init; } = [];
}