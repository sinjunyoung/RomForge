﻿using NSW.Core;
using RomForge.Core.ViewModels;
using Res = NSW.Properties.Resources;

namespace RomForge.ViewModels.Switch;

public class SwitchMainViewModel : MultiToolTabViewModel
{
    public RepackMainViewModel RepackVM { get; } = new();

    public MergeMainViewModel MergeVM { get; } = new();

    public SplitMainViewModel SplitVM { get; } = new();

    public Lz4MergeMainViewModel Lz4MergeVM { get; } = new();

    public ConverterMainViewModel ConverterVM { get; } = new();

    public ConvertSaturnMainViewModel ConvertSaturnVM { get; } = new();

    public KeygenMainViewModel KeygenVM { get; } = new();

    public RenameMainViewModel RenameVM { get; } = new();

    public static bool KeysAvailable => KeySetProvider.Instance.KeySet != null;

    public static string KeysMissingMessage => Res.Main_Err_NoKeys;

    public bool CanUseTools => IsIdle && KeysAvailable;

    public SwitchMainViewModel()
    {
        Tools.Add(RepackVM);
        Tools.Add(MergeVM);        
        Tools.Add(SplitVM);
        Tools.Add(Lz4MergeVM);
        Tools.Add(ConverterVM);
        Tools.Add(ConvertSaturnVM);
        Tools.Add(KeygenVM);
        Tools.Add(RenameVM);

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