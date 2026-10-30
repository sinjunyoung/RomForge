using PSP.PBP.Services;
using System.Collections.ObjectModel;

namespace RomForge.Core.ViewModels.PSP;

public record FmvFixOption(string Label, uint Value);

public class PopsOptionsViewModel : ViewModelBase
{
    public PopsOptionsViewModel() => SelectedFmvFix = FmvFixPresets[0];

    public ObservableCollection<FmvFixOption> FmvFixPresets { get; } = [new("0x04 (다수 게임에서 검증됨)", 0x04), new("0x07 (실험적)", 0x07)];

    public FmvFixOption SelectedFmvFix { get; set => SetProperty(ref field, value); }

    public bool UseFmvFix { get; set => SetProperty(ref field, value); }

    public bool UseCdTimingFix { get; set => SetProperty(ref field, value); }

    public byte[]? BuildConfig(byte[]? presetConfig)
    {
        if (presetConfig != null || (!UseFmvFix && !UseCdTimingFix))
            return presetConfig;

        var fmvValue = UseFmvFix ? SelectedFmvFix.Value : 0;

        return ExternalConfigBuilder.Build(UseFmvFix, fmvValue, UseCdTimingFix);
    }
}