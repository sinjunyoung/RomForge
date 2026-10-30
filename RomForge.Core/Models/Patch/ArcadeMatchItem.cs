using RomForge.Core.ViewModels;

namespace RomForge.Core.Models.Patch;

public class ArcadeMatchItem : ProcessableItemBase
{
    public string SourceFileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;

    public string? PatchFileName { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsMatched)); } }
        
    public Entry? PatchEntry { get; set { SetProperty(ref field, value); } }

    public string? MismatchReason { get; set { SetProperty(ref field, value); } }

    public bool IsMatched => PatchFileName is not null;
}