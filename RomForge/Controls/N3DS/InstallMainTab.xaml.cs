using Microsoft.Win32;
using PickPack.Disk.Helpers;
using RomForge.Core.Services;
using RomForge.ViewModels.N3DS;
using System.Windows;
using System.Windows.Controls;

namespace RomForge.Controls.N3DS;

public partial class InstallMainTab : UserControl
{
    private InstallerMainViewModel ViewModel => (InstallerMainViewModel)DataContext;
    private UsbDetector _usbDetector = null!;
    private bool _suppressSelectionChanged;

    public InstallMainTab()
    {
        InitializeComponent();
        Loaded += MainTab_Loaded;
    }

    private void MainTab_Loaded(object sender, RoutedEventArgs e)
    {
        if (_usbDetector != null) 
            return;

        if (ViewModel == null) 
            return;

        _ = InitializeDrivesAsync();

        Window parentWindow = Window.GetWindow(this);

        if (parentWindow != null)
        {
            _usbDetector = new UsbDetector();

            _usbDetector.Register(parentWindow);

            _usbDetector.DeviceChanged += async () =>
            {
                await Task.Delay(500);
                await Dispatcher.InvokeAsync(InitializeDrivesAsync);
            };
        }
    }

    private async Task InitializeDrivesAsync()
    {
        if (ViewModel == null)
            return;

        _suppressSelectionChanged = true;

        try
        {
            var infos = await Task.Run(() => DriveInfos.GetDriveInfos());

            SdDriveComboBox.Items.Clear();

            if (infos == null || infos.Count == 0)
            {
                ViewModel.SdPath = string.Empty;
                MovablePathBox.Text = string.Empty;
                SdDriveComboBox.IsEnabled = false;
                return;
            }

            SdDriveComboBox.IsEnabled = true;

            foreach (var info in infos)
                SdDriveComboBox.Items.Add(info);

            int lastIndex = SdDriveComboBox.Items.Count - 1;

            SdDriveComboBox.SelectedIndex = lastIndex;

            if (SdDriveComboBox.Items[lastIndex] is DriveInfos lastDrive)
            {
                ViewModel.SdPath = lastDrive.DriveLetter!;

                await ViewModel.CheckAndSetMovablePathAsync(ViewModel.SdPath!);

                MovablePathBox.Text = ViewModel.MovablePath;
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private async void SdDriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged || ViewModel == null) 
            return;

        if (SdDriveComboBox.SelectedItem is not DriveInfos selectedItem) 
            return;

        ViewModel.SdPath = selectedItem.DriveLetter!;

        if (string.IsNullOrEmpty(ViewModel.SdPath)) 
            return;

        await ViewModel.CheckAndSetMovablePathAsync(ViewModel.SdPath);

        MovablePathBox.Text = ViewModel.MovablePath;
    }

    private void BrowseMovable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "movable.sed 선택",
            Filter = "movable.sed|movable.sed|모든 파일|*.*",
            FileName = "movable.sed",
        };

        if (dialog.ShowDialog() != true) 
            return;

        ViewModel.MovablePath = dialog.FileName;
        MovablePathBox.Text = ViewModel.MovablePath;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.CancelExtract();
}