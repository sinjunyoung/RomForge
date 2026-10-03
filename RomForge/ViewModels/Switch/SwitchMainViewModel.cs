﻿using NSW.Core;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class SwitchMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public MergeMainViewModel MergeVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public ConvertSaturnMainViewModel ConvertSaturnVM { get; } = new();

    public KeygenMainViewModel KeygenVM { get; } = new();

    public static bool KeysAvailable => KeySetProvider.Instance.KeySet != null;

    public static string KeysMissingMessage => Res.Main_Err_NoKeys;

    public bool CanUseTools => IsIdle && KeysAvailable;

    public SwitchMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(MergeVM);
        Tools.Add(ConverterVM);
        Tools.Add(ConvertSaturnVM);
        Tools.Add(KeygenVM);

        InitializeMultiTools();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsIdle))
                OnPropertyChanged(nameof(CanUseTools));
        };

        RefreshKeysStatus();
    }

    public void RefreshKeysStatus()
    {
        KeySetProvider.Instance.TryLoadKeys();

        OnPropertyChanged(nameof(KeysAvailable));
        OnPropertyChanged(nameof(CanUseTools));
    }
}