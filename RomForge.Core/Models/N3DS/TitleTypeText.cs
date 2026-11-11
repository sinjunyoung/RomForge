using Common.Localization;
using N3DS.Core.Enums;

namespace RomForge.Core.Models.N3DS;

public static class TitleTypeText
{
    public static string Of(TitleType type) => type switch
    {
        TitleType.Application => Loc.S.ButtonFilterGame,
        TitleType.SystemApplication => Loc.S.TitleTypeSystem,
        TitleType.Patch => Loc.S.ButtonFilterUpdate,
        TitleType.DlcContent => "DLC",
        _ => Loc.S.LabelOther
    };
}