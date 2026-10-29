using Common;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RomForge.Core.Services;
using RomForge.Core.Models.PSP;
using RomForge.Core.Services.PSP;
using PSP.PBP.Enums;
using PSP.PBP.Services;
using RomForge.Core;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.PSP;

public class UnpackingMainViewModel : ToolTabViewModel
{
    private CancellationTokenSource _cts = new();

    public ObservableCollection<PbpFileItem> FileItems { get; } = [];

    public bool IsConverting { get; set { SetProperty(ref field, value); CommandManager.InvalidateRequerySuggested(); } }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<PbpFileItem>? ScrollToItemRequested;

    public UnpackingMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    public async Task AddPaths(IEnumerable<string> paths)
    {
        try
        {
            var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var path in Common.Utils.ExpandPaths(paths))
            {
                if (!existing.Add(path))
                    continue;

                var vm = new PbpFileItem(path);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                var reader = new PbpReader(stream);
                var meta = GameMetadataLookup.Find(reader.Discs[0].DiscID);

                vm.TitleName = meta?.ETitle;
                vm.TitleLocalName = meta?.LTitle;
                vm.Languages = meta?.Languages ?? [];
                vm.TitleId = string.Join(", ", reader.Discs.Select(d => d.DiscID));

                if (PbpReader.TryGetResourceStream(ResourceType.ICON0, stream, out var iconStream))
                    vm.Icon = iconStream?.ToBitmapImage();

                FileItems.Add(vm);

                for (int i = 0; i < FileItems.Count; i++)
                    FileItems[i].No = i + 1;

                OnPropertyChanged(nameof(HintVisibility));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch(Exception ex)
        {
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }
    }

    public void RemoveItems(IEnumerable<PbpFileItem> items)
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

                AppendLog($"총 {totalCount}개의 언팩 작업을 시작합니다.", LogLevel.Highlight);

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

                    var progressHandler = new Progress<ProgressInfo>(p => { item.Progress = p.Percent; });
                    string inputExt = item.Extension;

                    try
                    {
                        _cts.Token.ThrowIfCancellationRequested();

                        if (File.Exists(item.FilePath))
                        {
                            var unpacker = new PbpUnpacker
                            {
                                OnNotify = msg => AppendLog(msg),
                                OnProgress = percent => item.Progress = percent
                            };

                            string outputDir = Path.GetDirectoryName(item.FilePath)!;

                            await unpacker.UnpackAsync(item.FilePath, outputDir, true, _cts.Token);
                        }

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
                        AppendLog($"[{item.FileName}.{item.Extension}] 변환 실패: {ex.Message}", LogLevel.Error);
                        await SoundPlayer.PlayErrorAsync();
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

            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();
                MainViewModel.SetTaskbarProgress(0);

                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
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