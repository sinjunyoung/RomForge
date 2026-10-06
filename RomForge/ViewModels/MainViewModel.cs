using Common;
using Common.WPF.ViewModels;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.ViewModels._3DS;
using RomForge.ViewModels.Patch;
using RomForge.ViewModels.PS;
using RomForge.ViewModels.Settings;
using RomForge.ViewModels.Switch;
using RomForge.ViewModels.Util;
using RomForge.ViewModels.Wii;
using RomForge.ViewModels.WiiU;
using System.Collections.ObjectModel;
using System.Windows.Shell;

namespace RomForge.ViewModels;

public class MainViewModel : ToolTabViewModel
{
    private int _selectedTabIndex;

    public static double LogBoxHeight
    {
        get => AppConfig.Instance.Common.LogBoxHeight;
        set { AppConfig.Instance.Common.LogBoxHeight = value; }
    }

    public PatchMainViewModel PatchVM { get; }

    public CompressMainViewModel CompressVM { get; } = new();

    public ConvertMainViewModel UnifiedConvertVM { get; } = new();

    public SwitchMainViewModel SwitchMainVM { get; } = new();    

    public WiiUMainViewModel WiiUMainVM { get; } = new();

    public WiiMainViewModel WiiMainVM { get; } = new();

    public _3DSMainViewModel Main3DsVM { get; } = new();    

    public PS1MainViewModel PSMainVM { get; } = new();

    public UtilMainViewModel UtilMainVM { get; } = new();

    public SettingsMainViewModel Settings { get; } = new();

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveLogEntries));

            if (Tools[_selectedTabIndex] == SwitchMainVM)
                SwitchMainVM?.RefreshKeysStatus();
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
        2 => UnifiedConvertVM.LogEntries,
        3 => SwitchMainVM.LogEntries,
        4 => WiiUMainVM.LogEntries,
        5 => WiiMainVM.LogEntries,
        6 => Main3DsVM.LogEntries,        
        7 => PSMainVM.LogEntries,
        8 => UtilMainVM.LogEntries,
        _ => PatchVM.LogEntries
    };

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";

    private double _taskbarProgressValue;
    public double TaskbarProgressValue
    {
        get => _taskbarProgressValue;
        set
        {
            _taskbarProgressValue = value;
            OnPropertyChanged(nameof(TaskbarProgressValue));
        }
    }

    private TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;
    public TaskbarItemProgressState TaskbarProgressState
    {
        get => _taskbarProgressState;
        set
        {
            _taskbarProgressState = value;
            OnPropertyChanged(nameof(TaskbarProgressState));
        }
    }

    private static MainViewModel Current { get; set; }

    public MainViewModel()
    {
        Current = this;

        PatchVM = new PatchMainViewModel(async (file) => await MapsToHashAndProcess(file));
        SwitchMainVM.MergeVM.SettingsClicked += async (s, e) => await NavigateSwitchCompressSettings();
        Main3DsVM.RunNavigateCerts += MainVM_RunNavigateCerts;
        UnifiedConvertVM.RunNavigateCerts += MainVM_RunNavigateCerts;
        PSMainVM.RunNavigatePackingSettings += PS1MainVM_RunNavigatePackingSettings;

        Tools.Add(PatchVM);
        Tools.Add(CompressVM);
        Tools.Add(UnifiedConvertVM);
        Tools.Add(SwitchMainVM);
        Tools.Add(WiiUMainVM);
        Tools.Add(WiiMainVM);
        Tools.Add(Main3DsVM);        
        Tools.Add(PSMainVM);
        Tools.Add(UtilMainVM);
        Tools.Add(Settings);

        foreach (var tool in Tools)
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

    private async Task NavigateSwitchCompressSettings()
    {
        SelectedViewModel = Settings;
        Settings.SelectedViewModel = Settings.Compress;
        Settings.Compress.SelectedTabIndex = 1;
    }

    private void MainVM_RunNavigateCerts(object? sender, EventArgs e)
    {
        SelectedViewModel = UtilMainVM;
        UtilMainVM.SelectedViewModel = UtilMainVM.CertsVM;
    }

    private void PS1MainVM_RunNavigatePackingSettings(object? sender, EventArgs e)
    {
        SelectedViewModel = Settings;
        Settings.SelectedViewModel = Settings.PS1;
    }

    public static void SaveConfig() => AppConfig.Instance.Save();

    public bool IsAnyChildLocked()
    {
        if (Tools.Any(vm => vm.IsLocked))
            return true;


        foreach (var child in Tools)
        {
            if (child.Tools != null && child.Tools.Any(child => child.IsLocked))
                return true;
        }

        return false;
    }

    public static void SetTaskbarProgress(double value, TaskbarItemProgressState state = TaskbarItemProgressState.Normal)
    {
        if (Current == null) return;

        if (value <= 0 || state == TaskbarItemProgressState.None)
        {
            Current.TaskbarProgressState = TaskbarItemProgressState.None;
            Current.TaskbarProgressValue = 0;
        }
        else
        {
            Current.TaskbarProgressState = state;
            Current.TaskbarProgressValue = value / 100.0;
        }
    }

    public void ClearLog() => ActiveLogEntries.Clear();
}