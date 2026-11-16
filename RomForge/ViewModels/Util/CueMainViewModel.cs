using Common.Models;
using Common.Localization;
using Common.Services;
using RomForge.Core.Models.Util;
using RomForge.Core.Services;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Util;

public class CueMainViewModel : BatchToolTabViewModel
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".bin" };

    private CancellationTokenSource _cts = new();

    public ObservableCollection<CueFileItem> FileItems { get; } = [];

    public bool IsConverting { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); } }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public ICommand RunCommand { get; }

    public event Action<CueFileItem>? ScrollToItemRequested;

    public CueMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Utils.ExpandPaths(paths))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                continue;

            if (!existing.Add(path))
                continue;

            try
            {
                var vm = new CueFileItem(path);

                FileItems.Add(vm);

                for (int i = 0; i < FileItems.Count; i++)
                    FileItems[i].No = i + 1;
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogAddFailed(path, ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
            }
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<CueFileItem> items)
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

    public static string GetFileDialogFilter()
    {
        string wildcards = string.Join(";", SupportedExtensions.Select(ext => $"*{ext}"));
        return $"{Loc.S.FilterSupportedFiles}|{wildcards}|{Loc.S.FilterAllFiles}|*.*";
    }

    private async Task RunAsync()
    {
        IsConverting = true;

        _cts.Dispose();

        _cts = new CancellationTokenSource();

        ClearLog();

        bool canceled = false;
        bool failed = false;

        using (BeginWork())
        {
            try
            {
                int totalCount = FileItems.Count;
                int successCount = 0;

                AppendLog(Loc.S.LogCueStart(totalCount), LogLevel.Highlight);

                var targets = FileItems.Where(i => i.State != ItemStatus.Done).ToList();

                BeginBatch(targets.Count);

                foreach (var item in targets)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    item.Progress = 0;
                    item.State = ItemStatus.Converting;

                    StartBatchItem();

                    ScrollToItemRequested?.Invoke(item);

                    bool success = await Task.Run(() => ProcessSingleFile(item, _cts.Token));

                    if (success)
                    {
                        item.Progress = 100;
                        item.State = ItemStatus.Done;
                        successCount++;

                        CompleteBatchItem();
                    }
                    else
                    {
                        item.Progress = 0;

                        CompleteBatchItem(false);
                        await SoundPlayer.PlayErrorAsync();

                        if (item.State is ItemStatus.Converting)
                            item.State = ItemStatus.Failed;
                    }
                }

                if (successCount > 0)
                {
                    AppendLog(Loc.S.LogJobSummary(successCount, totalCount), LogLevel.Highlight);
                    await SoundPlayer.PlayCompleteAsync();
                }
                else
                    AppendLog(Loc.S.LogNoSuccessfulJobs, LogLevel.Error);

            }
            catch (OperationCanceledException)
            {
                canceled = true;

                AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
                await SoundPlayer.PlayCancelAsync();

                foreach (var item in FileItems.Where(i => i.State is ItemStatus.Waiting or ItemStatus.Converting))
                {
                    item.State = ItemStatus.Cancelled;
                    item.Progress = 0;
                }
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                failed = true;

                foreach (var item in FileItems.Where(i => i.State is ItemStatus.Converting))
                    item.State = ItemStatus.Failed;
            }
            finally
            {
                IsConverting = false;

                EndBatch(canceled, failed);
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool ProcessSingleFile(CueFileItem item, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            string targetCuePath = Path.Combine(item.Directory, item.TargetName);

            if (File.Exists(targetCuePath))
            {
                item.State = ItemStatus.Unsupported;

                AppendLog(Loc.S.LogCueSkipExists(item.TargetName), LogLevel.Error);
                return false;
            }

            string trackMode = DetectBinTrackMode(item.FilePath);

            AppendLog(Loc.S.LogCueAnalyzed(item.FileName, trackMode));

            string binFileNameWithExt = Path.GetFileName(item.FilePath);
            var sb = new StringBuilder();

            sb.AppendLine($"FILE \"{binFileNameWithExt}\" BINARY");
            sb.AppendLine($"  TRACK 01 {trackMode}");
            sb.AppendLine("    INDEX 01 00:00:00");
            ct.ThrowIfCancellationRequested();
            File.WriteAllText(targetCuePath, sb.ToString(), Encoding.UTF8);
            AppendLog(Loc.S.LogCueCreated(item.TargetName));

            return true;
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogCueItemFailed(item.FileName, ex.Message), LogLevel.Error);
            return false;
        }
    }

    private static string DetectBinTrackMode(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (fs.Length < 16)
                return "MODE1/2352";

            byte[] header = new byte[16];
            int read = fs.Read(header, 0, 16);

            if (read < 16)
                return "MODE1/2352";

            byte modeByte = header[15];

            return modeByte switch
            {
                1 => "MODE1/2352",
                2 => "MODE2/2352",
                _ => "MODE1/2352"
            };
        }
        catch
        {
            return "MODE1/2352";
        }
    }
}