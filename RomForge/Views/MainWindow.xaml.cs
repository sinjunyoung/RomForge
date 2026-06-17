using DiscUtils.Iso9660;
using PBP.Core;
using PBP.Core.Cue;
using PBP.Core.Models;
using PBP.Core.Readers;
using RomForge.Helpers;
using RomForge.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace RomForge.Views;

public partial class MainWindow : Window
{

    private MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
        Closing += MainWindow_Closing;

        //var converter = new PbpConverter(new PbpPackOptions { CompressionLevel = 9 });

        //converter.Convert(
        //    [
        //    (DiskSource.FromBinCue("D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 1)_patched.bin", "D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 1).cue"), "Valkyrie Profile"),
        //    (DiskSource.FromBinCue("D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 2) (v1.1)_patched.bin", "D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 2) (v1.1).cue"), "Valkyrie Profile"),
        //], "Valkyrie Profile.pbp");

        //converter.Convert(
        //    [
        //        (DiskSource.FromChd("D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 1)_patched.chd"), "Valkyrie Profile"),
        //        (DiskSource.FromChd("D:\\download\\발키리 프로파일\\Valkyrie Profile (Japan) (Disc 2) (v1.1)_patched.chd"), "Valkyrie Profile"),
        //    ], "Valkyrie Profile.pbp");
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;
        int value = 1;

        _ = Win32API.DwmSetWindowAttribute(hWnd, 20, ref value, sizeof(int));
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        ViewModel.SaveConfig();
        bool busy = ViewModel.CompressVM.IsLocked || ViewModel.PatchVM.IsLocked;

        if (!busy)
            return;

        var result = MessageBox.Show("작업이 진행 중입니다. 취소하고 종료할까요?", "종료 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ViewModel.CompressVM.CancelCommand.Execute(null);
            ViewModel.PatchVM.CancelCommand.Execute(null);
        }
        else
            e.Cancel = true;
    }
}