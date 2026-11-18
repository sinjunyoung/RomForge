using PSP.PBP.Services;
using Common.Localization;
using System.Collections.ObjectModel;

namespace RomForge.Core.ViewModels.PSP;

public class PopsOptionsViewModel : ViewModelBase
{
    public PopsOptionsViewModel() => SelectedFmvFix = FmvFixPresets[0];

    public ObservableCollection<FmvFixOption> FmvFixPresets { get; } = [new(0x04, () => Loc.S.LabelVerifiedOnManyGames), new(0x07, () => Loc.S.LabelExperimental)];

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