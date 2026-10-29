using Common;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.Services.PSVita;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels.PSVita;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;

namespace RomForge.ViewModels.PSVita;

public class ConverterMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    public ObservableCollection<SourceRowViewModel> SourceRows { get; } = [];

    public string? OutputPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.VitaConvertOutputPath = value;
        }
    }

    public Visibility OutputHintVisibility => string.IsNullOrWhiteSpace(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesHintVisibility => SourceRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool OutputZip { get; set { SetProperty(ref field, value); } } = true;

    public bool OutputFolder { get; set { SetProperty(ref field, value); } }

    public SourceRowViewModel? SelectedRow { get; set { SetProperty(ref field, value); } }

    public ICommand RemoveSelectedCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public ConverterMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.VitaConvertOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.VitaConvertOutputPath;

        RemoveSelectedCommand = new RelayCommand(_ => RemoveRow(SelectedRow), _ => SelectedRow != null);
        RemoveAllCommand = new RelayCommand(_ => SourceRows.Clear(), _ => SourceRows.Count > 0);
        BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
        CancelCommand = new RelayCommand(_ => Cancel());

        var view = CollectionViewSource.GetDefaultView(SourceRows);

        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SourceRowViewModel.TitleId)));
        view.SortDescriptions.Add(new SortDescription(nameof(SourceRowViewModel.TitleId), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(SourceRowViewModel.Category), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(SourceRowViewModel.ContentIdSuffix), ListSortDirection.Ascending));

        SourceRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(EntriesHintVisibility));
    }

    private void BrowseOutput()
    {
        var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
        {
            Description = "출력 폴더 선택",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.SelectedPath;
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        foreach (string path in ExpandPaths(paths))
        {
            if (!path.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"{Path.GetFileName(path)}: PKG 파일만 추가할 수 있습니다.", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                continue;
            }

            if (SourceRows.Any(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            SourceRowViewModel row;

            try
            {
                row = await Task.Run(() => SourceRowViewModel.FromPkg(path));
            }
            catch (Exception ex)
            {
                AppendLog($"{Path.GetFileName(path)}: 분석 실패 - {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                continue;
            }

            ApplyLicenseFile(row, path);
            AddOrReplaceRow(row);

            _ = row.LoadMetadataAsync();
        }
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.EnumerateFiles(path, "*.pkg", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    yield return file;
            }
            else if (File.Exists(path))
                yield return path;
        }
    }

    private static void ApplyLicenseFile(SourceRowViewModel? row, string pkgPath)
    {
        string? dir = Path.GetDirectoryName(pkgPath);

        if (!string.IsNullOrEmpty(dir))
        {
            string txtPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(pkgPath) + ".txt");

            if (File.Exists(txtPath))
            {
                try
                {
                    string txtContent = File.ReadAllText(txtPath).Trim();

                    if (!string.IsNullOrEmpty(txtContent))
                        row?.License = txtContent;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(row?.License))
            {
                var group = NPSService.Instance.GetGroup(row?.TitleId);

                row?.License = row?.Category switch
                {
                    ContentCategory.App => group?.App?.ZRif,
                    ContentCategory.Addcont => group?.GetDlcLicense(row.ContentIdSuffix),
                    _ => row?.License
                };
            }
        }
    }

    private void AddOrReplaceRow(SourceRowViewModel newRow)
    {
        var duplicate = newRow.Category == ContentCategory.Addcont
            ? SourceRows.FirstOrDefault(r => r.Category == ContentCategory.Addcont && string.Equals(r.TitleId, newRow.TitleId, StringComparison.OrdinalIgnoreCase) && string.Equals(r.ContentIdSuffix, newRow.ContentIdSuffix, StringComparison.OrdinalIgnoreCase))
            : SourceRows.FirstOrDefault(r => r.Category == newRow.Category && string.Equals(r.TitleId, newRow.TitleId, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            AppendLog($"{duplicate.FileName} -> {newRow.FileName}(으)로 대체됨 ({newRow.TitleId} {newRow.Category})");
            SourceRows.Remove(duplicate);
        }

        SourceRows.Add(newRow);
    }

    private void RemoveRow(SourceRowViewModel? row)
    {
        if (row != null)
            SourceRows.Remove(row);
    }

    public async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            AppendLog("출력 폴더를 선택하세요.", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (SourceRows.Count == 0)
        {
            AppendLog("PKG 파일을 하나 이상 추가하세요.", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                await ExecuteAsync(CancellationTokenSource.Token);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            var output = OutputZip ? PkgConvertOutput.Zip : PkgConvertOutput.Folder;
            var entries = SourceRows.Select(r => r.ToBatchEntry()).ToList();

            AppendLog($"NoNpDrm 포맷({(OutputZip ? "zip" : "폴더")})으로 변환 중...", LogLevel.Highlight);

            var outputs = await NoNpDrmConvertService.ConvertAsync(entries, OutputPath!, output, AppendLog, BuildProgressReporter(), ct);

            AppendLog($"전체 완료: {outputs.Count}개 타이틀 ({_totalSw.Elapsed:mm\\:ss})", LogLevel.Ok);
            await SoundPlayer.PlayCompleteAsync();
            OutputPath?.OpenFolder();
        }
        catch (OperationCanceledException)
        {
            AppendLog("작업이 취소되었습니다.", LogLevel.Error);
            await SoundPlayer.PlayCancelAsync();
            MainViewModel.SetTaskbarProgress(0);
        }
        catch (Exception ex)
        {
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
        }
    }
}