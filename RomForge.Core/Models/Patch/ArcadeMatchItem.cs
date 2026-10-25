using RomForge.Core.ViewModels;

namespace RomForge.Core.Models.Patch;

public class ArcadeMatchItem : ProcessableItemBase
{
    public string SourceFileName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;

    private string? _patchFileName;
    public string? PatchFileName
    {
        get => _patchFileName;
        set
        {
            _patchFileName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMatched));
        }
    }

    private Entry? _patchEntry;
    public Entry? PatchEntry
    {
        get => _patchEntry;
        set { _patchEntry = value; OnPropertyChanged(); }
    }

    private string? _mismatchReason;
    public string? MismatchReason
    {
        get => _mismatchReason;
        set { _mismatchReason = value; OnPropertyChanged(); }
    }

    public bool IsMatched => PatchFileName is not null;
}