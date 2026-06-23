using CHD.Core.Services;
using Common;
using Common.WPF.ViewModels;
using DolphinTool.Core.Services;
using Patch.Core;
using RomForge.Models;
using RomZip.Core.Enums;
using RomZip.Core.Models;
using RomZip.Core.Services;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Patch;

public class NormalPatchMainViewModel : ToolTabViewModel
{
    private readonly Core.AppConfig _config;
    private CancellationTokenSource? _runCts;
    private string? _outputCuePath;
    private List<string> _copiedTrackPaths = [];

    public System.Collections.ObjectModel.ObservableCollection<LogEntry> LogEntries { get; } = [];

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchLabel));            
        }
    }

    public bool AutoCompress
    {
        get => _config.Patch.AutoCompress;
        set
        {
            _config.Patch.AutoCompress = value;
            OnPropertyChanged(nameof(AutoCompress));
        }
    }

    public string SourceLabel => Path.GetFileName(SourcePath) ?? "원본 파일을 드래그하거나 클릭하세요";
    public string PatchLabel => Path.GetFileName(PatchPath) ?? "패치 파일을 드래그하거나 클릭하세요";

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private string _progressStatus = "대기 중";
    public string ProgressStatus
    {
        get => _progressStatus;
        set { _progressStatus = value; OnPropertyChanged(); }
    }

    public NormalPatchMainViewModel(Core.AppConfig config)
    {
        _config = config;

        _config.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Core.AppConfig.Patch))
                OnPropertyChanged(nameof(AutoCompress));
        };

        _config.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Core.PatchConfig.AutoCompress))
                OnPropertyChanged(nameof(AutoCompress));
        };
    }

    public void Log(string message, LogLevel level)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = message, Level = level }));
    }

    public async Task RunAsync()
    {
        if (SourcePath is null || PatchPath is null)
            return;

        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        Progress = 0;
        ProgressStatus = "패치 중...";
        _outputCuePath = null;
        _copiedTrackPaths = [];

        string outputDir = Path.Combine(Path.GetDirectoryName(SourcePath)!, "output");
        string outputPath = Path.Combine(outputDir, Path.GetFileName(SourcePath));

        Log($"패치 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        try
        {
            Directory.CreateDirectory(outputDir);

            var detected = FormatDetector.Detect(SourcePath);
            var sourceLength = new FileInfo(SourcePath).Length;
            var patchLength = new FileInfo(PatchPath).Length;
            bool useBytes = sourceLength < UniversalPatcher.MemoryThreshold && patchLength < UniversalPatcher.MemoryThreshold;

            await PatchAsync(detected, outputDir, outputPath, useBytes, ct);

            ProgressStatus = "완료";
        }
        catch (OperationCanceledException)
        {
            Progress = 0;
            ProgressStatus = "취소됨";
            Log($"패치 취소: {SourcePath}", LogLevel.Error);
            Cleanup(outputPath);
        }
        catch (Exception ex)
        {
            Progress = 0;
            ProgressStatus = "실패";
            Log($"패치 실패: {ex.Message}", LogLevel.Error);
            Cleanup(outputPath);
        }
    }

    private async Task PatchAsync(DetectResult detected, string outputDir, string outputPath, bool useBytes, CancellationToken ct)
    {
        bool isZipTarget = detected.Format is not (RomFormat.Bin or RomFormat.Iso or RomFormat.Gcm or RomFormat.Wii or RomFormat.Wbfs);

        if (AutoCompress && isZipTarget && useBytes)
        {
            var patched = await UniversalPatcher.ApplyPatchAsync(SourcePath!, PatchPath!, p => Progress = (int)(p * 100), ct);
            Progress = 100;
            Log($"패치 완료", LogLevel.Ok);
            await CompressToZipFromBytesAsync(patched, outputDir, ct);
            return;
        }

        await UniversalPatcher.ApplyPatchAsync(SourcePath!, PatchPath!, outputPath, p => Progress = (int)(p * 100), ct);
        Progress = 100;
        Log($"패치 완료: {outputPath}", LogLevel.Ok);

        if (detected.Format == RomFormat.Bin)
            _outputCuePath = await CopyBinTracksAsync(outputDir, outputPath);

        if (!AutoCompress)
            return;

        if (isZipTarget)
            await CompressToZipFromFileAsync(outputPath, outputDir, ct);
        else
            await CompressKnownAsync(detected, outputPath, ct);
    }

    private async Task<string?> CopyBinTracksAsync(string outputDir, string outputPath)
    {
        string? cuePath = Directory.GetFiles(Path.GetDirectoryName(SourcePath!)!, "*.cue")
            .FirstOrDefault(c => ConversionSource.ParseBinsFromCue(c)
                .Any(b => string.Equals(Path.GetFileName(b), Path.GetFileName(SourcePath), StringComparison.OrdinalIgnoreCase)));

        if (cuePath is null)
        {
            Log("CUE 파일을 찾을 수 없습니다.", LogLevel.Error);
            return null;
        }

        var sourceDir = Path.GetDirectoryName(cuePath)!;
        var referencedBins = ConversionSource.ParseBinsFromCue(cuePath);

        foreach (var binName in referencedBins)
        {
            string sourceBinPath = Path.Combine(sourceDir, Path.GetFileName(binName));
            string targetBinPath = Path.Combine(outputDir, Path.GetFileName(binName));

            if (!string.Equals(targetBinPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(sourceBinPath))
                {
                    File.Copy(sourceBinPath, targetBinPath, true);
                    _copiedTrackPaths.Add(targetBinPath);
                }
                else
                {
                    Log($"멀티 트랙 파일을 찾을 수 없습니다: {Path.GetFileName(sourceBinPath)}", LogLevel.Error);
                    return null;
                }
            }
        }

        string outputCuePath = Path.Combine(outputDir, Path.GetFileName(cuePath));
        File.Copy(cuePath, outputCuePath, true);
        return outputCuePath;
    }

    private async Task CompressToZipFromBytesAsync(byte[] patched, string outputDir, CancellationToken ct)
    {
        ProgressStatus = "압축 중...";
        Progress = 0;
        Log($"압축 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        string zipPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(SourcePath) + ".zip");

        await Task.Run(() =>
        {
            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(Path.GetFileName(SourcePath!));
            using var entryStream = entry.Open();

            long totalBytes = patched.Length;
            long bytesWrittenTotal = 0;
            int chunkSize = 81920;

            while (bytesWrittenTotal < totalBytes)
            {
                ct.ThrowIfCancellationRequested();
                int bytesToWrite = (int)Math.Min(chunkSize, totalBytes - bytesWrittenTotal);
                entryStream.Write(patched, (int)bytesWrittenTotal, bytesToWrite);
                bytesWrittenTotal += bytesToWrite;
                if (totalBytes > 0)
                    Progress = (int)((double)bytesWrittenTotal / totalBytes * 100);
            }
        }, ct);

        Log($"압축 완료: {zipPath}", LogLevel.Ok);
    }

    private async Task CompressToZipFromFileAsync(string outputPath, string outputDir, CancellationToken ct)
    {
        ProgressStatus = "압축 중...";
        Progress = 0;
        Log($"압축 시작: {Path.GetFileName(SourcePath)}", LogLevel.Highlight);

        string zipPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(SourcePath) + ".zip");

        await Task.Run(() =>
        {
            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(Path.GetFileName(SourcePath!));
            using var entryStream = entry.Open();

            using var sourceStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[81920];
            long totalBytes = sourceStream.Length;
            long bytesReadTotal = 0;
            int bytesRead;

            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                entryStream.Write(buffer, 0, bytesRead);
                bytesReadTotal += bytesRead;
                if (totalBytes > 0)
                    Progress = (int)((double)bytesReadTotal / totalBytes * 100);
            }
        }, ct);

        File.Delete(outputPath);
        Log($"압축 완료: {zipPath}", LogLevel.Ok);
    }

    private async Task CompressKnownAsync(DetectResult detected, string outputPath, CancellationToken ct)
    {
        switch (detected.Format)
        {
            case RomFormat.Bin:
                {
                    ProgressStatus = "CHD 변환 중...";
                    Progress = 0;

                    FileConverter converter = new();
                    converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                    converter.ProgressChanged += (_, e) => Progress = e.Progress;

                    var chdResult = await converter.ConvertFileAsync(_outputCuePath!, ct);

                    if (!chdResult.Success)
                        throw new Exception($"CHD 변환 실패: {chdResult.Message}");

                    File.Delete(outputPath);
                    File.Delete(_outputCuePath!);
                    _outputCuePath = null;

                    foreach (var trackPath in _copiedTrackPaths)
                        if (File.Exists(trackPath))
                            File.Delete(trackPath);

                    _copiedTrackPaths.Clear();
                    break;
                }
            case RomFormat.Iso:
                {
                    ProgressStatus = "CHD 변환 중...";
                    Progress = 0;

                    FileConverter converter = new();
                    converter.LogMessage += (_, e) => Log(e.Message, e.Level);
                    converter.ProgressChanged += (_, e) => Progress = e.Progress;

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
                    ProgressStatus = "포맷 변환 중...";
                    Progress = 0;

                    DolphinService dolphin = new();
                    dolphin.LogMessage += (_, e) => Log(e.Message, e.Level);
                    dolphin.ProgressChanged += (_, e) => Progress = e.Progress;

                    await dolphin.ConvertFileAsync(outputPath, detected.Format.ToString(), detected.OutputExtension, _config.Dolphin.CompressLevel, ct);
                    File.Delete(outputPath);
                    break;
                }
        }
    }

    private void Cleanup(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            if (_outputCuePath is not null && File.Exists(_outputCuePath))
                File.Delete(_outputCuePath);

            foreach (var trackPath in _copiedTrackPaths)
                if (File.Exists(trackPath))
                    File.Delete(trackPath);
        }
        catch (Exception ex)
        {
            Log($"파일 정리 실패: {ex.Message}", LogLevel.Error);
        }
    }

    public void Cancel() => _runCts?.Cancel();

    public void Clear()
    {
        _runCts?.Cancel();
        SourcePath = null;
        PatchPath = null;
        Progress = 0;
        ProgressStatus = "대기 중";
        AutoCompress = false;
        LogEntries.Clear();
    }
}