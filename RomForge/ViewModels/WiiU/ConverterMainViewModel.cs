using Common;
using RomForge.Core;
using RomForge.Core.Models.WiiU;
using RomForge.Core.Services;
using RomForge.Core.Services.WiiU;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WiiU.Core.Services;

namespace RomForge.ViewModels.WiiU;

public class ConverterMainViewModel : ToolTabViewModel
{
    private bool _isConverting;
    private CancellationTokenSource _cts = new();
    private string _outputPath;

    private static readonly HashSet<string> SupportedExtensions = [".wud", ".wux", ".wua"];

    private static string KeysPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys.txt");

    public ObservableCollection<FileItem> FileItems { get; } = [];

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputHintVisibility));
            AppConfig.Instance.OutputFolders.WiiUConvertOutputPath = value;
        }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public ICommand BrowseOutputCommand { get; }

    public event Action<FileItem>? ScrollToItemRequested;

    public ConverterMainViewModel()
    {
        OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.WiiUConvertOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.WiiUConvertOutputPath;
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
        BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput());
    }

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolvedPaths = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                if (IsGameFolder(path))
                    resolvedPaths.Add(path);
                else
                    CollectValidPaths(path, resolvedPaths);
            }
            else if (File.Exists(path))
                resolvedPaths.Add(path);
        }

        foreach (var path in resolvedPaths)
        {
            bool isFolder = Directory.Exists(path);

            if (!isFolder && !SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                continue;

            if (!existing.Add(path))
                continue;

            var item = new FileItem(path);

            if (item.SelectedTargetFormat == "미지원")
                continue;

            await LoadMetadataAsync(item);

            FileItems.Add(item);

            for (int i = 0; i < FileItems.Count; i++)
                FileItems[i].No = i + 1;
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    private static bool IsGameFolder(string dir)
    {
        try
        {
            return Directory.Exists(Path.Combine(dir, "code")) || Directory.Exists(Path.Combine(dir, "meta")) || Directory.Exists(Path.Combine(dir, "content"));
        }
        catch
        {
            return false;
        }
    }

    private static void CollectValidPaths(string currentDir, List<string> results)
    {
        try
        {
            foreach (var file in Directory.GetFiles(currentDir))
            {
                if (SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    results.Add(file);
            }

            foreach (var subDir in Directory.GetDirectories(currentDir))
            {
                if (IsGameFolder(subDir))
                    results.Add(subDir);
                else
                    CollectValidPaths(subDir, results);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (Exception) { }
    }

    public void RemoveItems(IEnumerable<FileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    private static async Task LoadMetadataAsync(FileItem item)
    {
        try
        {
            if (Directory.Exists(item.FilePath))
            {
                await Task.Run(() =>
                {
                    using ITitleSource source = item.Extension == "wup" ? new WupTitleSource(item.FilePath) : new FolderTitleSource(item.FilePath);
                    item.TitleIdHex = source.TitleIdHex;
                    item.TitleVersion = source.TitleVersion;
                    var meta = item.Extension == "wup" ? MetadataExtractor.ExtractFromTitleSource(source) : MetadataExtractor.ExtractFromFolder(item.FilePath);

                    ApplyMetadata(item, meta);
                });
            }
            else
            {
                var meta = await MetadataExtractor.Extract(item.FilePath, KeysPath);

                ApplyMetadata(item, meta);

                await Task.Run(() =>
                {
                    var sources = Converter.OpenSources(item.FilePath, KeysPath);

                    try
                    {
                        if (sources.Count > 0)
                        {
                            item.TitleIdHex = sources[0].TitleIdHex;
                            item.TitleVersion = sources[0].TitleVersion;
                        }
                    }
                    finally
                    {
                        foreach (var s in sources)
                            s.Dispose();
                    }
                });
            }
        }
        catch { }
    }

    private static void ApplyMetadata(FileItem item, ExtractorMetadata? meta)
    {
        if (meta is null)
            return;

        item.TitleName = meta.Title;

        if (meta.Image is { Length: > 0 } pngBytes)
            item.Icon = TryLoadIcon(pngBytes);
    }

    private static BitmapImage? TryLoadIcon(byte[] pngBytes)
    {
        try
        {
            return pngBytes.ToBitmapImage();
        }
        catch
        {
            return null;
        }
    }

    private async Task RunAsync()
    {
        if (string.IsNullOrEmpty(OutputPath))
        {
            AppendLog("출력 폴더를 선택하세요.", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            return;
        }

        IsConverting = true;

        _cts.Dispose();

        _cts = new CancellationTokenSource();

        ClearLog();

        using (BeginWork())
        {
            try
            {
                Directory.CreateDirectory(OutputPath);

                await Task.Run(async () =>
                {
                    int totalCount = FileItems.Count;

                    AppendLog($"총 {totalCount}개의 WiiU 변환 작업을 시작합니다.", LogLevel.Highlight);

                    int cnt = 0;

                    foreach (var item in FileItems)
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        if (item.Status is "완료" or "미지원")
                            continue;

                        item.Status = "대기중";
                        item.Progress = 0;
                        MainViewModel.SetTaskbarProgress(0);
                        item.Status = "변환중";

                        ScrollToItemRequested?.Invoke(item);

                        try
                        {
                            ConvertOne(item, _cts.Token);

                            item.Progress = 100;
                            MainViewModel.SetTaskbarProgress(100);
                            item.Status = "완료";
                            cnt++;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"[{item.FileName}] 변환 실패: {ex.Message}", LogLevel.Error);
                            MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                            item.Status = "실패";
                            item.Progress = 0;                            
                        }
                    }

                    if (cnt > 0)
                    {
                        AppendLog($"총 {cnt}개의 작업을 성공적으로 완료했습니다.", LogLevel.Ok);
                        await SoundPlayer.PlayCompleteAsync();
                        MainViewModel.SetTaskbarProgress(100);
                    }
                    else
                        AppendLog("성공한 작업이 없습니다.", LogLevel.Error);

                }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);
                foreach (var item in FileItems.Where(i => i.Status is "대기중" or "변환중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);

                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
            finally
            {
                IsConverting = false;

                CleanupTask();
            }
        }
    }

    private void ConvertOne(FileItem item, CancellationToken ct)
    {
        var sources = Converter.OpenSources(item.FilePath, KeysPath);

        try
        {
            string outputRoot = OutputPath;
            int total = sources.Count;

            for (int i = 0; i < sources.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var source = sources[i];
                string outputName = Converter.BuildOutputName(source, item.TitleName);

                void OnFileProgress(int done, int totalFiles, string label)
                {
                    int subPercent = totalFiles > 0 ? (int)(done * 100.0 / totalFiles) : 100;
                    item.Progress = (int)(((i * 100.0) + subPercent) / total);
                }

                switch (item.SelectedTargetFormat)
                {
                    case "WUP":
                        {
                            string outFolder = Utils.GetUniqueFolderPath(Path.Combine(outputRoot, $"{outputName} [WUP]"));
                            Converter.ConvertToWup(source, outFolder, OnFileProgress, ct);
                            break;
                        }

                    case "Loadiine":
                        {
                            string outFolder = Utils.GetUniqueFolderPath(Path.Combine(outputRoot, $"{outputName} [Loadiine]"));
                            Converter.ConvertToLoadiine(source, outFolder, OnFileProgress, ct);
                            break;
                        }

                    case "WUA":
                        {
                            string outFile = Utils.GetUniqueFilePath(Path.Combine(outputRoot, $"{outputName}.wua"));
                            Converter.ConvertToWua(source, outFile, OnFileProgress, ct);
                            break;
                        }

                    default:
                        throw new NotSupportedException($"지원하지 않는 출력 포맷입니다: {item.SelectedTargetFormat}");
                }
            }
        }
        finally
        {
            foreach (var s in sources)
                s.Dispose();
        }
    }

    private async Task BrowseOutput()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "출력 폴더 선택" };

        if (dlg.ShowDialog() == true)
            OutputPath = dlg.FolderName;

        await Task.CompletedTask;
    }
}