using Common;
using Common.WPF.ViewModels;
using PBP.Core.Enums;
using PBP.Core.Services;
using RomForge.Core.Services.PS;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PS;

public class UnpackingMainViewModel : ToolTabViewModel
{
    #region Fields

    private bool _isConverting;
    private CancellationTokenSource _cts = new();

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<PbpFileItem> FileItems { get; } = [];

    #endregion

    #region Properties

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    public ICommand RunCommand { get; }

    #endregion

    public event Action<PbpFileItem>? ScrollToItemRequested;

    #region Constructor

    public UnpackingMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    #endregion

    #region Public Methods

    public async Task AddPaths(IEnumerable<string> paths)
    {
        try
        {
            var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var path in ExpandPaths(paths))
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
                {
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();
                    bitmap.StreamSource = iconStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    vm.Icon = bitmap;
                }

                vm.No = FileItems.Count + 1;

                FileItems.Add(vm);

                OnPropertyChanged(nameof(HintVisibility));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        catch(Exception ex)
        {
            AppendLog($"오류: {ex.Message}", LogLevel.Error);
        }
    }

    public void RemoveItems(IEnumerable<PbpFileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    #endregion

    #region Private Methods

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

                            string uniqueName = Utils.GetUniqueFilePath(item.FilePath);

                            await unpacker.UnpackAsync(uniqueName, Path.GetDirectoryName(item.FilePath), true, _cts.Token);
                        }

                        item.Progress = 100;
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
                        item.Status = "실패";
                        item.Progress = 0;
                    }
                }

                if (cnt > 0)
                {
                    AppendLog($"총 {cnt}개의 작업을 성공적으로 완료했습니다.", LogLevel.Ok);
                }
                else
                {
                    AppendLog("성공한 작업이 없습니다.", LogLevel.Error);
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
            finally
            {
                IsConverting = false;
            }
        }
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", options))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = msg, Level = level })
        );
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}