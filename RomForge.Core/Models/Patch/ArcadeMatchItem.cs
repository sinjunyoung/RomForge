using Common.Localization;
using RomForge.Core.ViewModels;

namespace RomForge.Core.Models.Patch;

public class ArcadeMatchItem : ProcessableItemBase
{
    public string SourceFileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;

    public string? PatchFileName { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsMatched)); } }
        
    public Entry? PatchEntry { get; set { SetProperty(ref field, value); } }

    public ArcadeMatchKind MatchKind { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(MismatchReason)); } }

    public string? MatchDetail { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(MismatchReason)); } }

    public string? MismatchReason => MatchKind switch
    {
        ArcadeMatchKind.CrcMatched => Loc.S.LabelCrcMatch,
        ArcadeMatchKind.CrcMismatch => Loc.S.LabelCrcMismatch,
        ArcadeMatchKind.PatchNotRegistered => Loc.S.LabelDatPatchNotRegistered(MatchDetail ?? string.Empty),
        _ => null
    };

    public bool IsMatched => PatchFileName is not null;
}