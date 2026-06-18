using Common.WPF.ViewModels;
using RomForge.Core.Models.Patch;

namespace RomForge.ViewModels.Patch;

public class ArcadeMatchItem : ViewModelBase
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

    private PatchEntry? _patchEntry;
    public PatchEntry? PatchEntry
    {
        get => _patchEntry;
        set { _patchEntry = value; OnPropertyChanged(); }
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private string? _mismatchReason;
    public string? MismatchReason
    {
        get => _mismatchReason;
        set { _mismatchReason = value; OnPropertyChanged(); }
    }

    public bool IsMatched => PatchFileName is not null;
}