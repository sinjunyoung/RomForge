using PSP.PBP.Services;
using RomForge.Core.ViewModels;
using System.Collections.ObjectModel;

namespace RomForge.ViewModels.PSP;

public record FmvFixOption(string Label, uint Value);

public class PopsOptionsViewModel : ViewModelBase
{
    private FmvFixOption _selectedFmvFix;
    private bool _useFmvFix;
    private bool _useCdTimingFix;

    public PopsOptionsViewModel() => _selectedFmvFix = FmvFixPresets[0];

    public ObservableCollection<FmvFixOption> FmvFixPresets { get; } = [new("0x04 (다수 게임에서 검증됨)", 0x04), new("0x07 (실험적)", 0x07)];

    public FmvFixOption SelectedFmvFix
    {
        get => _selectedFmvFix;
        set => SetProperty(ref _selectedFmvFix, value);
    }

    public bool UseFmvFix
    {
        get => _useFmvFix;
        set => SetProperty(ref _useFmvFix, value);
    }

    public bool UseCdTimingFix
    {
        get => _useCdTimingFix;
        set => SetProperty(ref _useCdTimingFix, value);
    }

    public byte[]? BuildConfig(byte[]? presetConfig)
    {
        if (presetConfig != null || (!UseFmvFix && !UseCdTimingFix))
            return presetConfig;

        var fmvValue = UseFmvFix ? SelectedFmvFix.Value : 0;

        return ExternalConfigBuilder.Build(UseFmvFix, fmvValue, UseCdTimingFix);
    }
}