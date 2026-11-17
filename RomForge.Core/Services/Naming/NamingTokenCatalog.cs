using Common.Localization;
using RomForge.Core.Models.Naming;

namespace RomForge.Core.Services.Naming;

public static class NamingTokenCatalog
{
    private static NamingTokenOption[] LanguageOptions => [
        new("code1", Loc.S.LabelNamingDigits(1), "1"),
        new("code2", Loc.S.LabelNamingDigits(2), "2"),
        new("code3", Loc.S.LabelNamingDigits(3), "3"),
        new(null, Loc.S.LabelNamingFullName, string.Empty)
    ];

    private static NamingTokenOption[] DateOptions => [
        new("yyMMdd", $"{Loc.S.LabelNamingDigits(6)} (yyMMdd)", "6"),
        new("yyyyMMdd", $"{Loc.S.LabelNamingDigits(8)} (yyyyMMdd)", "8")
    ];

    public static IReadOnlyList<NamingTokenDefinition> Patch => [
        new("fileName", Loc.S.HeaderFileName, Loc.S.NamingTokFileNameDesc),
        new("Lan1", Loc.S.NamingTokSourceLangLabel, Loc.S.NamingTokSourceLangDesc, LanguageOptions, ["Language"]),
        new("Lan2", Loc.S.NamingTokPatchLangLabel, Loc.S.NamingTokPatchLangDesc, LanguageOptions),
        new("Version", Loc.S.HeaderVersion, Loc.S.NamingTokPatchVersionDesc),
        new("Date", Loc.S.NamingTokDateLabel, Loc.S.NamingTokDateDesc, DateOptions)
    ];

    public static IReadOnlyList<NamingTokenDefinition> Rename => [
        new("Name", Loc.S.NamingTokNameLabel, Loc.S.NamingTokNameDesc),
        new("KTitleName", Loc.S.NamingTokKTitleLabel, Loc.S.NamingTokKTitleDesc),
        new("TitleName", Loc.S.HeaderTitle, Loc.S.NamingTokTitleNameDesc),
        new("TitleID", Loc.S.NamingTokTitleIdLabel, Loc.S.NamingTokTitleIdDesc),
        new("Tag", Loc.S.NamingTokTagLabel, Loc.S.NamingTokTagDesc),
        new("DisplayVersion", Loc.S.NamingTokDisplayVersionLabel, Loc.S.NamingTokDisplayVersionDesc),
        new("Version", Loc.S.HeaderVersion, Loc.S.NamingTokRawVersionDesc)
    ];
}