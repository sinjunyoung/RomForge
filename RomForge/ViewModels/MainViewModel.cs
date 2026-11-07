using Common.Services;
using RomForge.Controls.Util;
using RomForge.Core;
using RomForge.Core.Models;
using RomForge.Core.UI.Helpers;
using RomForge.Core.ViewModels;
using RomForge.ViewModels.N3DS;
using RomForge.ViewModels.Patch;
using RomForge.ViewModels.PSP;
using RomForge.ViewModels.PSVita;
using RomForge.ViewModels.Settings;
using RomForge.ViewModels.Switch;
using RomForge.ViewModels.Util;
using RomForge.ViewModels.Wii;
using RomForge.ViewModels.WiiU;
using RomForge.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace RomForge.ViewModels;

public class MainViewModel : ToolTabViewModel
{
    public static ImageSource? AppIcon
    {
        get
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                if (string.IsNullOrEmpty(exePath))
                    return null;

                using var icon = Icon.ExtractAssociatedIcon(exePath);

                if (icon == null)
                    return null;

                return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch
            {
                return null;
            }
        }
    }

    public static double LogBoxHeight { get => AppConfig.Instance.Common.LogBoxHeight; set { AppConfig.Instance.Common.LogBoxHeight = value; } }

    public PatchMainViewModel PatchVM { get; }

    public CompressMainViewModel CompressVM { get; } = new();

    public ConvertMainViewModel UnifiedConvertVM { get; } = new();

    public SwitchMainViewModel SwitchMainVM { get; } = new();    

    public WiiUMainViewModel WiiUMainVM { get; } = new();

    public WiiMainViewModel WiiMainVM { get; } = new();

    public N3DSMainViewModel Main3DsVM { get; } = new();

    public PSVitaMainViewModel PSVitaMainVM { get; } = new();

    public PSPMainViewModel PSPMainVM { get; } = new();    

    public UtilMainViewModel UtilMainVM { get; } = new();

    public SettingsMainViewModel Settings { get; } = new();

    public int SelectedTabIndex
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(ActiveLogEntries));
            OnPropertyChanged(nameof(HasHelpKey));

            if (Tools[field] == SwitchMainVM)
                SwitchMainVM?.RefreshKeysStatus();
        }
    }

    public ToolTabViewModel SelectedViewModel
    {
        get => Tools[SelectedTabIndex];
        set
        {
            var index = Tools.IndexOf(value);

            if (index != -1)
                SelectedTabIndex = index;
        }
    }

    public ObservableCollection<LogEntry> ActiveLogEntries => SelectedTabIndex switch
    {
        0 => PatchVM.LogEntries,
        1 => CompressVM.LogEntries,
        2 => UnifiedConvertVM.LogEntries,
        3 => SwitchMainVM.LogEntries,
        4 => WiiUMainVM.LogEntries,
        5 => WiiMainVM.LogEntries,
        6 => Main3DsVM.LogEntries,
        7 => PSVitaMainVM.LogEntries,
        8 => PSPMainVM.LogEntries,
        9 => UtilMainVM.LogEntries,
        _ => PatchVM.LogEntries
    };

    public static string AppVersion => $"{AppDomain.CurrentDomain.FriendlyName} - Ver {Utils.ToAppVersionString()}";
        
    public double TaskbarProgressValue { get; set => SetProperty(ref field, value); }

    public TaskbarItemProgressState TaskbarProgressState { get; set => SetProperty(ref field, value); } = TaskbarItemProgressState.None;

    public override bool HasHelpKey => SelectedViewModel?.HasHelpKey ?? false;

    public override string? HelpPageKey => SelectedViewModel?.HelpPageKey;

    internal static MainViewModel? Current { get; set; }

    public MainViewModel()
    {
        Current = this;

        PatchVM = new PatchMainViewModel(async (file) => await MapsToHashAndProcess(file));
        Main3DsVM.RunNavigateCerts += MainVM_RunNavigateCerts;
        UnifiedConvertVM.RunNavigateCerts += MainVM_RunNavigateCerts;
        PSPMainVM.RunNavigatePackingSettings += PS1MainVM_RunNavigatePackingSettings;

        Tools.Add(PatchVM);
        Tools.Add(CompressVM);
        Tools.Add(UnifiedConvertVM);
        Tools.Add(SwitchMainVM);
        Tools.Add(WiiUMainVM);
        Tools.Add(WiiMainVM);
        Tools.Add(Main3DsVM);
        Tools.Add(PSVitaMainVM);
        Tools.Add(PSPMainVM);
        Tools.Add(UtilMainVM);
        Tools.Add(Settings);

        foreach (var tool in Tools)
        {
            RegisterChild(tool);

            tool.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(HasHelpKey))
                    OnPropertyChanged(nameof(HasHelpKey));
            };
        }
    }

    public async Task MapsToHashAndProcess(string fileName)
    {        
        var tab = new HashTab();

        await UtilMainVM.HashVM.AddPaths([fileName]);

        var window = new BaseToolWindow(UtilMainVM.HashVM, tab, "해시 계산");
        window.Show();

        if (UtilMainVM.HashVM.RunCommand.CanExecute(null))
            UtilMainVM.HashVM.RunCommand.Execute(null);
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

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetTaskbarProgress(value, state));
            return;
        }

        if (value <= 0 || state == TaskbarItemProgressState.None)
        {
            Current.TaskbarProgressState = TaskbarItemProgressState.None;
            Current.TaskbarProgressValue = 0;
        }
        else
        {
            Current.TaskbarProgressState = state;
            Current.TaskbarProgressValue = value / 100.0;

            if (state == TaskbarItemProgressState.Error)
                FlashTaskbar.FlashTaskbarIcon(Application.Current?.MainWindow!);
        }
    }

    public override void ClearLog() => ActiveLogEntries.Clear();
}