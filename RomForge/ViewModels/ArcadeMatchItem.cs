using Common.WPF.ViewModels;
using RomForge.Core.Models;

namespace RomForge.ViewModels;

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

    public bool IsMatched => PatchFileName is not null;
}