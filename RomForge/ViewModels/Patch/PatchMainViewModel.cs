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
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class PatchMainViewModel : ToolTabViewModel
{
    private readonly Core.AppConfig _config;

    public NormalPatchViewModel NormalVM { get; }

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

    public PatchMainViewModel(Core.AppConfig config)
    {
        _config = config;
        NormalVM = new NormalPatchViewModel(_config);
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
        if (NormalVM.SourcePath is null || NormalVM.PatchPath is null) return;

        NormalVM.Progress = 0;
        Log($"패치 시작: {Path.GetFileName(NormalVM.SourcePath)}", LogLevel.Info);


        try
        {
            var sourceBytes = await File.ReadAllBytesAsync(NormalVM.SourcePath, ct);
            var patchBytes = await File.ReadAllBytesAsync(NormalVM.PatchPath, ct);
            var result = await Task.Run(() => UniversalPatcher.ApplyPatch(sourceBytes, patchBytes, p => NormalVM.Progress = (int)(p * 100)), ct);

            string outputDir = Path.Combine(Path.GetDirectoryName(NormalVM.SourcePath)!, "output");
            Directory.CreateDirectory(outputDir);

            if (NormalVM.AutoCompress)
            {
                var detected = FormatDetector.Detect(NormalVM.SourcePath);

                switch (detected.Format)
                {
                    case RomFormat.Bin:
                        {
                            string outputBinPath = Path.Combine(outputDir, Path.GetFileName(NormalVM.SourcePath));

                            await File.WriteAllBytesAsync(outputBinPath, result, ct);

                            string? cuePath = Directory.GetFiles(Path.GetDirectoryName(NormalVM.SourcePath)!, "*.cue")
                                .FirstOrDefault(c => ConversionSource.ParseBinsFromCue(c)
                                    .Any(b => string.Equals(Path.GetFileName(b), Path.GetFileName(NormalVM.SourcePath), StringComparison.OrdinalIgnoreCase)));

                            if (cuePath is null)
                            {
                                Log("CUE 파일을 찾을 수 없습니다.", LogLevel.Error);
                                return;
                            }

                            string outputCuePath = Path.Combine(outputDir, Path.GetFileName(cuePath));

                            File.Copy(cuePath, outputCuePath, true);

                            FileConverter converter = new();

                            converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                            converter.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            var chdResult = await converter.ConvertFileAsync(outputCuePath, ct);

                            if (!chdResult.Success)
                            {
                                Log($"CHD 변환 실패: {chdResult.Message}", LogLevel.Error);
                                return;
                            }

                            File.Delete(outputBinPath);
                            File.Delete(outputCuePath);

                            break;
                        }
                    case RomFormat.Iso:
                        {
                            string outputFilePath = Path.Combine(outputDir, Path.GetFileName(NormalVM.SourcePath));

                            await File.WriteAllBytesAsync(outputFilePath, result, ct);

                            FileConverter converter = new();

                            converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                            converter.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            var chdResult = await converter.ConvertFileAsync(outputFilePath, ct);

                            if (!chdResult.Success)
                            {
                                Log($"CHD 변환 실패: {chdResult.Message}", LogLevel.Error);
                                return;
                            }

                            File.Delete(outputFilePath);

                            break;
                        }
                    case RomFormat.Gcm:
                    case RomFormat.Wii:
                    case RomFormat.Wbfs:
                        {
                            string outputFilePath = Path.Combine(outputDir, Path.GetFileName(NormalVM.SourcePath));

                            await File.WriteAllBytesAsync(outputFilePath, result, ct);

                            DolphinService dolphin = new();

                            dolphin.LogMessage += (_, e) => Log(e.Message, e.Level);
                            dolphin.ProgressChanged += (_, e) => NormalVM.Progress = e.Progress;

                            await dolphin.ConvertFileAsync(outputFilePath, detected.Format.ToString(), detected.OutputExtension, _config.Dolphin.CompressLevel, ct);

                            File.Delete(outputFilePath);

                            break;
                        }
                    case RomFormat.Unknown:
                        {
                            string zipPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(NormalVM.SourcePath) + ".zip");

                            await Task.Run(() =>
                            {
                                using var zipStream = new FileStream(zipPath, FileMode.Create);
                                using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create);
                                var entry = archive.CreateEntry(Path.GetFileName(NormalVM.SourcePath));
                                using var entryStream = entry.Open();

                                entryStream.Write(result, 0, result.Length);
                            }, ct);

                            break;
                        }
                }
            }
            else
            {
                string outputPath = Path.Combine(outputDir, Path.GetFileName(NormalVM.SourcePath));

                await File.WriteAllBytesAsync(outputPath, result, ct);
            }

            NormalVM.Progress = 100;
            Log($"{Path.GetFileName(NormalVM.SourcePath)} 패치 완료.", LogLevel.Ok);

            if (Directory.Exists(outputDir))
                Process.Start("explorer.exe", $"\"{outputDir}\"");
        }
        catch (OperationCanceledException)
        {
            NormalVM.Progress = 0;
            Log($"패치 취소: {Path.GetFileName(NormalVM.SourcePath)}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            NormalVM.Progress = 0;
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
        }
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

        Log($"패치 시작: {Path.GetFileName(ArcadeVM.SourcePath)}", LogLevel.Info);

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
        App.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }
}