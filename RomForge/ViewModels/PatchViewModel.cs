using Common;
using Common.WPF.ViewModels;
using Patch.Core;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace RomForge.ViewModels;

public class PatchViewModel : ToolTabViewModel
{
    private readonly AppConfig _config;

    public NormalPatchViewModel NormalVM { get; } = new();

    public ArcadePatchViewModel ArcadeVM { get; } = new();

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public ObservableCollection<LogEntry> LogEntries { get; } = [];    

    public ICommand RunCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand ClearCommand { get; }

    private CancellationTokenSource? _cts;

    public PatchViewModel(AppConfig config)
    {
        _config = config;
        RunCommand = new RelayCommand(async _ => await RunAsync());
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
        ClearCommand = new RelayCommand(_ => Clear());
    }

    private async Task RunAsync()
    {
        _cts = new CancellationTokenSource();

        using (BeginWork())
        {
            switch (SelectedTabIndex)
            {
                case 0: await RunNormalAsync(_cts.Token); break;
                case 1: await RunArcadeAsync(_cts.Token); break;
            }
        }
    }

    private async Task RunNormalAsync(CancellationToken ct)
    {
        if (NormalVM.SourcePath is null || NormalVM.PatchPath is null) return;

        NormalVM.Progress = 0;
        NormalVM.StatusText = string.Empty;
        NormalVM.StatusColor = "#888888";

        Log($"패치 시작: [{Path.GetFileName(NormalVM.SourcePath)}]", LogLevel.Ok);

        try
        {
            var sourceBytes = await File.ReadAllBytesAsync(NormalVM.SourcePath, ct);
            var patchBytes = await File.ReadAllBytesAsync(NormalVM.PatchPath, ct);
            var result = await Task.Run(() => UniversalPatcher.ApplyPatch(sourceBytes, patchBytes, p => NormalVM.Progress = (int)(p * 100)), ct);
            var outputPath = PatchService.ResolveNormalOutputPath(NormalVM.SourcePath, NormalVM.PatchPath, _config.Patch.OutputFolder);

            await File.WriteAllBytesAsync(outputPath, result, ct);

            NormalVM.Progress = 100;
            NormalVM.StatusText = "완료";
            NormalVM.StatusColor = "#4CAF50";
            Log($"[{Path.GetFileName(outputPath)}] 패치 완료", LogLevel.Ok);
        }
        catch (OperationCanceledException)
        {
            NormalVM.StatusText = "취소됨";
            NormalVM.StatusColor = "#888888";
            Log("패치 취소됨", LogLevel.Info);
        }
        catch (Exception ex)
        {
            NormalVM.StatusText = $"실패: {ex.Message}";
            NormalVM.StatusColor = "#F44336";
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task RunArcadeAsync(CancellationToken ct)
    {
        var matched = ArcadeVM.MatchItems.Where(x => x.IsMatched).ToList();
        if (matched.Count == 0) return;

        foreach (var item in matched)
        {
            item.Progress = 0;
            item.Status = string.Empty;
        }

        await Parallel.ForEachAsync(matched,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (item, token) =>
            {
                try
                {
                    await PatchService.ApplyAsync(
                        item.SourcePath, item.PatchPath!, 
                        _config.Patch.OutputFolder,
                        new Progress<ProgressInfo>(p =>
                        {
                            item.Progress = p.Percent;
                            ArcadeVM.UpdateTotalProgress();
                        }),
                        null, token);

                    item.Progress = 100;
                    item.Status = "완료";
                    item.StatusColor = "#4CAF50";
                }
                catch (OperationCanceledException)
                {
                    item.Status = "취소";
                    item.StatusColor = "#888888";
                }
                catch
                {
                    item.Status = "실패";
                    item.StatusColor = "#F44336";
                }
                finally
                {
                    ArcadeVM.UpdateTotalProgress();
                }
            });
    }

    private void Clear()
    {
        switch (SelectedTabIndex)
        {
            case 0: NormalVM.Clear(); break;
            case 1: ArcadeVM.Clear(); break;
        }
    }

    private void Log(string message, LogLevel level)
    {
        App.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }
}