using Common;
using PSP.Core.Models;
using PSP.Core.Services;
using RomForge.Core;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RomForge.Core.Services;
using RomForge.Core.Models.PSP;

namespace RomForge.ViewModels.PSP;

public class ConverterViewModel : ToolTabViewModel
{
    private bool _isConverting;
    private CancellationTokenSource _cts = new();
    private readonly CsoService _csoService = new();

    private static readonly HashSet<string> SupportedExtensions = [".iso", ".cso", ".zso", ".chd"];

    public ObservableCollection<FileItem> FileItems { get; } = [];

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<FileItem>? ScrollToItemRequested;

    public ConverterViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Utils.ExpandPaths(paths))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
                continue;

            if (!existing.Add(path))
                continue;

            var item = new FileItem(path);

            FileItems.Add(item);

            for (int i = 0; i < FileItems.Count; i++)
                FileItems[i].No = i + 1;
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
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

    private async Task RunAsync()
    {
        IsConverting = true;

        _cts.Dispose();

        _cts = new CancellationTokenSource();

        ClearLog();

        using (BeginWork())
        {
            try
            {
                int totalCount = FileItems.Count;

                AppendLog($"총 {totalCount}개의 변환 작업을 시작합니다.", LogLevel.Highlight);

                int cnt = 0;

                foreach (var item in FileItems)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (item.Status == "완료" || item.Status == "미지원")
                        continue;

                    item.Status = "대기중";
                    item.Progress = 0;
                    MainViewModel.SetTaskbarProgress(0);
                    item.Status = "변환중";

                    ScrollToItemRequested?.Invoke(item);

                    var progressHandler = new Progress<ProgressInfo>(p => item.Progress = p.Percent);
                    string inputExt = item.Extension.ToLowerInvariant();
                    string outputExt = item.SelectedTargetFormat.ToLowerInvariant();
                    string outPath = Path.ChangeExtension(item.FilePath, outputExt);

                    outPath = Utils.GetUniqueFilePath(outPath);

                    try
                    {
                        switch ((inputExt, outputExt))
                        {
                            case ("iso", "cso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressAsync(input, output, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("iso", "zso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressAsync(input, output, magic: CsoHeader.MagicZSO, isLz4: true, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("iso", "chd"):
                                await _csoService.CompressToChdAsync(item.FilePath, outPath, AppConfig.Instance.Chdman.Compression, progressHandler, _cts.Token);
                                break;

                            case ("cso", "iso"):
                            case ("zso", "iso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.DecompressAsync(input, output, progressHandler, _cts.Token);
                                break;

                            case ("cso", "zso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.TranscodeAsync(input, output, targetMagic: CsoHeader.MagicZSO, targetIsLz4: true, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("zso", "cso"):
                                await using (var input = File.OpenRead(item.FilePath))
                                await using (var output = File.Create(outPath))
                                    await CsoService.TranscodeAsync(input, output, targetMagic: CsoHeader.MagicCSO, targetIsLz4: false, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("cso", "chd"):
                            case ("zso", "chd"):
                                await _csoService.CompressCsoToChdAsync(item.FilePath, outPath, progressHandler, AppConfig.Instance.Chdman.Compression, _cts.Token);
                                break;

                            case ("chd", "iso"):
                                await _csoService.ExtractChdToIsoAsync(item.FilePath, outPath, progressHandler, _cts.Token);
                                break;

                            case ("chd", "cso"):
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressFromChdAsync(item.FilePath, output, version: 1, progress: progressHandler, ct: _cts.Token);
                                break;

                            case ("chd", "zso"):
                                await using (var output = File.Create(outPath))
                                    await CsoService.CompressFromChdAsync(item.FilePath, output, magic: CsoHeader.MagicZSO, isLz4: true, progress: progressHandler, ct: _cts.Token);
                                break;

                            default:
                                throw new NotSupportedException($"{inputExt} → {outputExt} 변환은 지원하지 않습니다.");
                        }

                        item.Progress = 100;
                        MainViewModel.SetTaskbarProgress(100);
                        item.Status = "완료";                        
                        cnt++;
                    }
                    catch (OperationCanceledException)
                    {
                        if (File.Exists(outPath))
                        {
                            try { File.Delete(outPath); } catch { }
                        }
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (File.Exists(outPath))
                        {
                            try { File.Delete(outPath); } catch { }
                        }

                        AppendLog($"[{item.FileName}] 변환 실패: {ex.Message}", LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
                        item.Status = "실패";
                        item.Progress = 0;
                        MainViewModel.SetTaskbarProgress(0);
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
}