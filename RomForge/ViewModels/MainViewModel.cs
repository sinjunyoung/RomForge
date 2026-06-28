using Common;
using Common.WPF.ViewModels;
using RomForge.Core;
using RomForge.Models;
using RomForge.ViewModels._3DS;
using RomForge.ViewModels.Patch;
using RomForge.ViewModels.PS1;
using RomForge.ViewModels.PSP;
using RomForge.ViewModels.Settings;
using RomForge.ViewModels.Switch;
using RomForge.ViewModels.Util;
using System.Collections.ObjectModel;

namespace RomForge.ViewModels;

public class MainViewModel : ToolTabViewModel
{
    private int _selectedTabIndex;
    private readonly AppConfig _config = new AppConfig().Load();

    public double LogBoxHeight
    {
        get => _config.Common.LogBoxHeight;
        set { _config.Common.LogBoxHeight = value; }
    }

    public PatchMainViewModel PatchVM { get; }

    public CompressMainViewModel CompressVM { get; }

    public SwitchMainViewModel SwitchMainVM { get; }

    public _3DSMainViewModel Main3DsVM { get; }

    public PS1MainViewModel PS1MainVM { get; }

    public PSPMainViewModel PSPMainVM { get; }

    public UtilMainViewModel UtilMainVM { get; }

    public SettingsMainViewModel Settings { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveLogEntries));
        }
    }

    public ToolTabViewModel SelectedViewModel
    {
        set
        {
            var index = Tools.IndexOf(value);

            if (index != -1)
                SelectedTabIndex = index;
        }
    }

    public ObservableCollection<LogEntry> ActiveLogEntries => _selectedTabIndex switch
    {
        0 => PatchVM.LogEntries,
        1 => CompressVM.LogEntries,
        2 => SwitchMainVM.LogEntries,
        3 => Main3DsVM.LogEntries,
        4 => PS1MainVM.LogEntries,
        5 => PSPMainVM.LogEntries,
        6 => UtilMainVM.LogEntries,
        _ => PatchVM.LogEntries
    };

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";

    public MainViewModel()
    {
        PatchVM = new PatchMainViewModel(_config, async (file) => await MapsToHashAndProcess(file));
        CompressVM = new CompressMainViewModel(_config);
        SwitchMainVM = new SwitchMainViewModel(_config);
        SwitchMainVM.MergeVM.SettingsClicked += async (s, e) => await NavigateCompressSettings();
        Main3DsVM = new _3DSMainViewModel();
        PS1MainVM = new PS1MainViewModel(_config);
        PS1MainVM.RunNavigatePackingSettings += PS1MainVM_RunNavigatePackingSettings;
        PSPMainVM = new PSPMainViewModel(_config);
        UtilMainVM = new UtilMainViewModel();
        Settings = new SettingsMainViewModel(_config);

        Tools.Add(PatchVM);
        Tools.Add(CompressVM);
        Tools.Add(SwitchMainVM);
        Tools.Add(Main3DsVM);
        Tools.Add(PS1MainVM);
        Tools.Add(PSPMainVM);
        Tools.Add(UtilMainVM);
        Tools.Add(Settings);

        foreach(var tool in Tools)
            RegisterChild(tool);
    }

    public async Task MapsToHashAndProcess(string fileName)
    {
        SelectedViewModel = UtilMainVM;
        UtilMainVM.SelectedViewModel = UtilMainVM.HashVM;

        await UtilMainVM.HashVM.AddPaths([fileName]);

        if (UtilMainVM.HashVM.RunCommand.CanExecute(null))
            UtilMainVM.HashVM.RunCommand.Execute(null);
    }

    public async Task NavigateCompressSettings()
    {
        SelectedViewModel = Settings;
        Settings.SelectedViewModel = Settings.Compress;
    }

    private void PS1MainVM_RunNavigatePackingSettings(object? sender, EventArgs e)
    {
        SelectedViewModel = Settings;
        Settings.SelectedViewModel = Settings.PS1;
    }

    public void SaveConfig() => _config.Save();

    public bool IsAnyChildLocked()
    {   
        if (Tools.Any(vm => vm.IsLocked))
            return true;

        
        foreach (var child in Tools)
        {            
            if (child.Tools != null && child.Tools.Any(child=>child.IsLocked))
                return true;
        }

        return false;
    }
}