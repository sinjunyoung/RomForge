using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using DolphinTool.Core.Services;
using Patch.Core;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services.Patch;
using RomForge.Helpers;
using RomForge.Models;
using RomZip.Core.Enums;
using RomZip.Core.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class PatchMainViewModel : ToolTabViewModel
{
    private readonly Core.AppConfig _config;

    public NormalPatchMainViewModel NormalVM { get; }

    public ArcadePatchMainViewModel ArcadeVM { get; } = new();

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

    public PatchMainViewModel(Core.AppConfig config)
    {
        _config = config;
        NormalVM = new NormalPatchMainViewModel(_config);
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
                case 0: await RunNormalAsync(_cts.Token); 
                    break;
                case 1: await RunArcadeAsync(_cts.Token); 
                    break;
            }
        }
    }

    private async Task RunNormalAsync(CancellationToken ct)
    {
        if (NormalVM.SourcePath is null || NormalVM.PatchPath is null) 
            return;

        NormalVM.Progress = 0;
        string outputDir = Path.Combine(Path.GetDirectoryName(NormalVM.SourcePath)!, "output");
        string outputPath = Path.Combine(outputDir, Path.GetFileName(NormalVM.SourcePath));
        string? outputCuePath = null;

        Log($"패치 시작: {Path.GetFileName(NormalVM.SourcePath)}", LogLevel.Highlight);

        try
        {
            Directory.CreateDirectory(outputDir);

            await Task.Run(() => UniversalPatcher.ApplyPatch(NormalVM.SourcePath, NormalVM.PatchPath, outputPath, p => NormalVM.Progress = (int)(p * 100)), ct);

            if (NormalVM.AutoCompress)
            {
                var detected = FormatDetector.Detect(NormalVM.SourcePath);

                switch (detected.Format)
                {
                    case RomFormat.Bin:
                        {
                            string? cuePath = Directory.GetFiles(Path.GetDirectoryName(NormalVM.SourcePath)!, "*.cue")
                                .FirstOrDefault(c => ConversionSource.ParseBinsFromCue(c)
                                    .Any(b => string.Equals(Path.GetFileName(b), Path.GetFileName(NormalVM.SourcePath), StringComparison.OrdinalIgnoreCase)));

                            if (cuePath is null)
                            {
                                Log("CUE 파일을 찾을 수 없습니다.", LogLevel.Error);
                                return;
                            }

                            outputCuePath = Path.Combine(outputDir, Path.GetFileName(cuePath));
                            File.Copy(cuePath, outputCuePath, true);

                            FileConverter converter = new();
                            converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                            converter.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            var chdResult = await converter.ConvertFileAsync(outputCuePath, ct);

                            if (!chdResult.Success) 
                                throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                            File.Delete(outputPath);
                            File.Delete(outputCuePath);
                            outputCuePath = null;
                            break;
                        }
                    case RomFormat.Iso:
                        {
                            FileConverter converter = new();
                            converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                            converter.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            var chdResult = await converter.ConvertFileAsync(outputPath, ct);

                            if (!chdResult.Success) 
                                throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                            File.Delete(outputPath);
                            break;
                        }
                    case RomFormat.Gcm:
                    case RomFormat.Wii:
                    case RomFormat.Wbfs:
                        {
                            DolphinService dolphin = new();
                            dolphin.LogMessage += (_, e) => Log(e.Message, e.Level);
                            dolphin.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            await dolphin.ConvertFileAsync(outputPath, detected.Format.ToString(), detected.OutputExtension, _config.Dolphin.CompressLevel, ct);
                            File.Delete(outputPath);
                            break;
                        }
                    case RomFormat.Unknown:
                        {
                            Log($"압축 시작: {Path.GetFileName(outputPath)}", LogLevel.Highlight);

                            string zipPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(NormalVM.SourcePath) + ".zip");
                            await Task.Run(() =>
                            {
                                using var zipStream = new FileStream(zipPath, FileMode.Create);
                                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
                                archive.CreateEntryFromFile(outputPath, Path.GetFileName(NormalVM.SourcePath));
                            }, ct);
                            File.Delete(outputPath);

                            Log($"압축 완료: {Path.GetFileName(outputPath)}", LogLevel.Ok);

                            break;
                        }
                }
            }

            NormalVM.Progress = 100;
            Log($"패치 완료: {Path.GetFileName(NormalVM.SourcePath)}", LogLevel.Ok);

            if (Directory.Exists(outputDir))
                Process.Start("explorer.exe", $"\"{outputDir}\"");
        }
        catch (OperationCanceledException)
        {
            NormalVM.Progress = 0;
            Log($"패치 취소: {Path.GetFileName(NormalVM.SourcePath)}", LogLevel.Error);
            Cleanup(outputPath, outputCuePath);
        }
        catch (Exception ex)
        {
            NormalVM.Progress = 0;
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
            Cleanup(outputPath, outputCuePath);
        }
    }

    private static void Cleanup(string outputPath, string? cuePath)
    {
        if (File.Exists(outputPath)) 
            File.Delete(outputPath);

        if (cuePath != null && File.Exists(cuePath)) 
            File.Delete(cuePath);
    }

    private async Task RunArcadeAsync(CancellationToken ct)
    {
        var matched = ArcadeVM.MatchItems.Where(x => x.IsMatched).ToList();

        if (matched.Count == 0 || ArcadeVM.SourcePath is null)
            return;

        if (ArcadeVM.MatchItems.Any(x => x.MismatchReason == "CRC 불일치"))
        {
            Log("CRC 불일치 항목이 있어 패치를 진행할 수 없습니다.", LogLevel.Error);
            return;
        }

        string outputDir = Path.Combine(Path.GetDirectoryName(ArcadeVM.SourcePath)!, "output");
        string outputZipPath = Path.Combine(outputDir, Path.GetFileName(ArcadeVM.SourcePath));

        Log($"패치 시작: {Path.GetFileName(ArcadeVM.SourcePath)}", LogLevel.Highlight);

        var itemsByEntryName = new Dictionary<string, ArcadeMatchItem>();
        var patchesByEntryName = new Dictionary<string, PatchEntry>();

        foreach (var item in matched)
        {
            var entryName = item.SourcePath.Split('|', 2)[1];

            itemsByEntryName[entryName] = item;
            patchesByEntryName[entryName] = item.PatchEntry!;
            item.Progress = 0;
        }

        var progressReporter = new Progress<EntryPatchProgress>(p =>
        {
            if (itemsByEntryName.TryGetValue(p.EntryName, out var item))
            {
                item.Progress = p.Percent;
                ArcadeVM.UpdateSummary();
                ArcadeVM.UpdateTotalProgress();
            }
        });

        try
        {
            await PatchService.ApplyPatchedZipAsync(ArcadeVM.SourcePath, outputZipPath, patchesByEntryName, progressReporter, Log, ct);

            Log($"{Path.GetFileName(ArcadeVM.SourcePath)} 패치 완료.", LogLevel.Ok);

            if (Directory.Exists(outputDir))
                Process.Start("explorer.exe", $"\"{outputDir}\"");
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(outputZipPath);
            Log($"패치 취소: {Path.GetFileName(ArcadeVM.SourcePath)}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            TryDeleteIncompleteOutput(outputZipPath);
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
        }
    }

    private void TryDeleteIncompleteOutput(string outputZipPath)
    {
        try
        {
            if (File.Exists(outputZipPath))
                File.Delete(outputZipPath);
        }
        catch (Exception ex)
        {
            Log($"중단된 결과 파일 삭제 실패: {ex.Message} (수동으로 확인해주세요: {outputZipPath})", LogLevel.Error);
        }
    }

    private void Clear()
    {
        switch (SelectedTabIndex)
        {
            case 0: NormalVM.Clear(); 
                break;
            case 1: ArcadeVM.Clear();
                break;
        }
    }

    private void Log(string message, LogLevel level)
    {
        App.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }
}