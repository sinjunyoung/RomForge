using Common;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core;
using RomForge.Core.Models.Switch;
using RomForge.Core.Services.Switch;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Res = NSW.Properties.Resources;
using RomForge.Core.UI.Command;
using RomForge.Core.Services;

namespace RomForge.ViewModels.Switch;

public class SplitMainViewModel : ProgressToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();

    private string _outputPath = string.Empty;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz", ".xcz" };

    private readonly List<SplitRowViewModel> _files = [];

    public ObservableCollection<SplitRowViewModel> Rows { get; } = [];

    public bool HasFiles => _files.Count > 0;

    private OutputFormat _outputFormat = OutputFormat.NSP;
    private bool? _allSelected = false;

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));

            AppConfig.Instance.OutputFolders.SwitchSplitOutputPath = value;
        }
    }

    public OutputFormat OutputFormat
    {
        get => _outputFormat;
        set { _outputFormat = value; OnPropertyChanged(); }
    }

    public bool? AllSelected
    {
        get => _allSelected;
        set
        {
            if (_allSelected != value)
            {
                _allSelected = value;

                OnPropertyChanged();
            }
        }
    }

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand OpenWorkSpaceCommand { get; }

    public SplitMainViewModel()
    {
        var folders = AppConfig.Instance.OutputFolders;

        if (!string.IsNullOrWhiteSpace(folders.SwitchSplitOutputPath))
            OutputPath = folders.SwitchSplitOutputPath;
        else if (!string.IsNullOrWhiteSpace(folders.SwitchMergeOutputPath))
            OutputPath = folders.SwitchMergeOutputPath;
        else
            OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

        OpenWorkSpaceCommand = new RelayCommand(_ => ExecuteOpenWorkSpace());
    }

    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        var candidates = await Task.Run(() => paths.Where(p => SupportedExtensions.Contains(Path.GetExtension(p))).ToList());
        var added = new List<SplitRowViewModel>();
        bool hasKey = KeySetProvider.Instance.KeySet != null;

        foreach (var path in candidates)
        {
            if (_files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var row = new SplitRowViewModel(path)
            {
                SizeBytes = GetFileLength(path),
                TypeText = hasKey ? Res.Status_Analyzing : Res.Status_NoKey
            };

            _files.Add(row);
            Rows.Add(row);
            added.Add(row);
        }

        OnPropertyChanged(nameof(HasFiles));

        foreach (var row in added)
        {
            if (_files.Contains(row))
                await AnalyzeAsync(row);
        }
    }

    private static long GetFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string TitleOf(MetadataResult meta) => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : string.Empty;

    private static string Letter(string tag) => tag switch
    {
        "Base" => "B",
        "Update" => "U",
        "DLC" => "D",
        _ => "?"
    };

    private static int TypeOrder(string tag) => tag switch
    {
        "Base" => 0,
        "Update" => 1,
        _ => 2
    };

    private static async Task AnalyzeAsync(SplitRowViewModel row)
    {
        var keySet = KeySetProvider.Instance.KeySet;

        if (keySet == null)
        {
            row.TypeText = Res.Status_NoKey;
            return;
        }

        row.TypeText = Res.Status_Analyzing;

        var metas = await Task.Run(() =>
        {
            try
            {
                return MetadataReader.GetMetadataFromContainer(keySet.Clone(), row.FilePath, true);
            }
            catch
            {
                return null;
            }
        });

        if (metas == null || metas.Count == 0)
        {
            row.TypeText = "?";
            return;
        }

        var ordered = metas.OrderBy(m => TypeOrder(m.GetTypeTag())).ThenBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).ToList();
        var main = ordered[0];
        string mainName = TitleOf(main);

        if (!string.IsNullOrEmpty(mainName))
            row.Name = mainName;

        row.Icon = ordered.FirstOrDefault(m => m.IconData != null)?.IconData?.ToBitmapImage();
        row.TitleId = main.TitleId;

        var updateMeta = ordered.FirstOrDefault(m => m.GetTypeTag().ToString().Contains("Patch", StringComparison.OrdinalIgnoreCase) || m.GetTypeTag().ToString().Contains("Update", StringComparison.OrdinalIgnoreCase));

        row.Version = updateMeta != null ? updateMeta.GetEffectiveDisplayVersion() : main.GetEffectiveDisplayVersion();
        row.TypeText = string.Join("+", ordered.Select(m => Letter(m.GetTypeTag())).Distinct());

        row.Children.Clear();

        if (ordered.Count > 1)
        {
            foreach (var meta in ordered)
            {
                string childName = TitleOf(meta);

                row.Children.Add(new SplitRowViewModel(row.FilePath, row)
                {
                    Name = string.IsNullOrEmpty(childName) ? row.Name : childName,
                    TitleId = meta.TitleId,
                    TypeText = Letter(meta.GetTypeTag()),
                    Version = meta.GetEffectiveDisplayVersion(),
                    SizeBytes = meta.Size,
                });
            }
        }

        row.Analyzed = true;

        row.NotifyStructureChanged();
    }

    public void ToggleExpand(SplitRowViewModel row)
    {
        if (!row.CanExpand)
            return;

        int index = Rows.IndexOf(row);

        if (index < 0)
            return;

        if (row.IsExpanded)
        {
            foreach (var child in row.Children)
                Rows.Remove(child);

            row.IsExpanded = false;
            return;
        }

        for (int i = 0; i < row.Children.Count; i++)
            Rows.Insert(index + 1 + i, row.Children[i]);

        row.IsExpanded = true;
    }

    public void RemoveRows(IEnumerable<SplitRowViewModel> selected)
    {
        var parents = selected.Select(r => r.Parent ?? r).Distinct().ToList();

        foreach (var parent in parents)
        {
            foreach (var child in parent.Children)
                Rows.Remove(child);

            Rows.Remove(parent);
            _files.Remove(parent);
        }

        OnPropertyChanged(nameof(HasFiles));
    }

    public void ClearAll()
    {
        Rows.Clear();
        _files.Clear();
        OnPropertyChanged(nameof(HasFiles));
    }

    public void SelectAll(bool isChecked)
    {
        foreach (var file in _files)
            file.SetAll(isChecked);
    }

    public void SelectExceptBase()
    {
        foreach (var file in _files)
            file.SetExceptBase();
    }

    public async Task SplitAsync()
    {
        foreach (var row in _files.Where(f => !f.Analyzed).ToList())
        {
            await AnalyzeAsync(row);

            if (!row.Analyzed)
                AppendLog($"{row.Name}: 타이틀 정보를 읽지 못해 건너뜁니다.", LogLevel.Error);
        }

        var jobs = new List<(string FilePath, IReadOnlySet<string> TitleIds)>();

        foreach (var file in _files.Where(f => f.Analyzed))
        {
            var ids = file.GetSelectedTitleIds();

            if (ids.Count > 0)
                jobs.Add((file.FilePath, ids));
        }

        if (jobs.Count == 0)
        {
            AppendLog("분리할 타이틀을 선택하세요.", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        string outputDir = OutputPath?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(outputDir))
        {
            AppendLog(Res.Main_Err_NoOutput, LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                CancellationTokenSource = new CancellationTokenSource();

                int resultCount = 0;
                var progress = BuildProgressReporter();                
                var config = AppConfig.Instance.Switch;
                bool compress = OutputFormat is OutputFormat.NSZ or OutputFormat.XCZ;
                bool asXci = OutputFormat is OutputFormat.XCI or OutputFormat.XCZ;
                void Log(string msg, LogLevel level, string id) => AppendLog(msg, level);

                for (int i = 0; i < jobs.Count; i++)
                {
                    CancellationTokenSource.Token.ThrowIfCancellationRequested();
                    resultCount += await SplitService.Split(jobs[i].FilePath, outputDir, compress, config.CompressLevel, config.UseBlockMode, config.VerifyCompress, config.ForceKeyGen0, asXci, i + 1, jobs.Count, progress, Log, jobs[i].TitleIds, CancellationTokenSource.Token);
                }

                AppendLog(string.Format(Res.Main_Log_AllComplete, _totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);

                if (resultCount > 0)
                {
                    AppendLog(Res.Main_Msg_SplitDone);
                    AppConfig.Instance.OutputFolders.SwitchSplitOutputPath?.OpenFolder();
                    await SoundPlayer.PlayCompleteAsync();
                    outputDir.OpenFolder();
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("분리가 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"{Res.Log_Error}: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
            }
            finally
            {
                CleanupTask();
            }
        }
    }

    private void ExecuteOpenWorkSpace()
    {
        var path = OutputPath?.Trim();

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        path.OpenFolder();
    }
}