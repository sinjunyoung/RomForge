using Common;
using Microsoft.Win32;
using NSW.Core.Services;
using RomForge.Core.Models._3DS;
using RomForge.Core.Services;
using RomForge.ViewModels._3DS;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RomForge.Controls._3DS;

public partial class InstallTab : UserControl
{
    private InstallerMainViewModel Vm => (InstallerMainViewModel)DataContext;
    private CancellationTokenSource? _installCts;

    public InstallTab()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is InstallerMainViewModel vm)
                InstallListView.ItemsSource = vm.Install.Items;
        };
    }

    private async void InstallList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) 
            return;

        var allFiles = files
            .SelectMany(path => Directory.Exists(path) ? GetFilesSafe(path) : [path])
            .ToList();

        try
        {
            await Vm.Install.AddFiles(allFiles);
        }
        catch (Exception ex)
        {
            Vm.AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
    }

    private void InstallList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void InstallList_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
            Vm.Install.RemoveItems(InstallListView.SelectedItems.Cast<TitleViewModel>());
    }

    private async void InstallAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "설치할 파일 선택",
            Filter = InstallMainViewModel.GetFileDialogFilter(),
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) 
            return;

        try
        {
            await Vm.Install.AddFiles(dialog.FileNames);
        }
        catch (Exception ex)
        {
            Vm.AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
    }

    private async void InstallAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "추가할 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await Vm.Install.AddFiles(GetFilesSafe(dialog.SelectedPath));
            }
            catch (Exception ex)
            {
                Vm.AppendLog($"오류: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }
    }

    private void InstallRemove_Click(object sender, RoutedEventArgs e) => Vm.Install.RemoveItems(InstallListView.SelectedItems.Cast<TitleViewModel>());

    private void InstallClear_Click(object sender, RoutedEventArgs e) => Vm.Install.Clear();

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        _installCts = new CancellationTokenSource();

        Vm.Install.IsInstalling = true;

        try
        {
            foreach (var item in InstallListView.Items)
            {
                if (item is not TitleViewModel title) 
                    continue;

                if (_installCts.IsCancellationRequested) 
                    continue;

                await Vm.InstallAsync(title, _installCts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Vm.AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
        finally
        {
            _installCts.Dispose();

            _installCts = null;
            Vm.Install.IsInstalling = false;
        }
    }

    private void InstallCancel_Click(object sender, RoutedEventArgs e) => _installCts?.Cancel();

    private static List<string> GetFilesSafe(string folder)
    {
        var files = new List<string>();

        try 
        {
            files.AddRange(Directory.GetFiles(folder)); 
        } 
        catch { }

        foreach (var subDir in Directory.GetDirectories(folder))
            try 
            { 
                files.AddRange(GetFilesSafe(subDir)); 
            } 
            catch { }

        return files;
    }

    private void LvFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (InstallListView.SelectedItems.Count == 0)
            e.Handled = true;
    }

    private void MenuItem_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = InstallListView.SelectedItems.Cast<TitleViewModel>().ToList();

        if (selected.Count == 0)
            return;

        Path.GetDirectoryName(selected[0].FilePath)?.OpenFolder();
    }
}