using Common;
using Common.WPF.ViewModels;
using RomForge.Core.Models;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Services;

namespace RomForge.ViewModels.Patch;

public class VitaPatchMainViewModel : ToolTabViewModel
{
    private readonly Stopwatch _totalSw = new();
    private CancellationTokenSource? _cts;

    private string _sourcePath = string.Empty;
    private string _patchPath = string.Empty;
    private string _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "vita");
    private bool _buildEmu = true;
    private bool _buildRetail;
    private int _progressPct;
    private string _progressLabel = string.Empty;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public string SourcePath
    {
        get => _sourcePath;
        set { _sourcePath = value; OnPropertyChanged(); }
    }

    public string PatchPath
    {
        get => _patchPath;
        set { _patchPath = value; OnPropertyChanged(); }
    }

    public string OutputPath
    {
        get => _outputPath;
        set { _outputPath = value; OnPropertyChanged(); }
    }

    public bool BuildEmu
    {
        get => _buildEmu;
        set { _buildEmu = value; OnPropertyChanged(); }
    }

    public bool BuildRetail
    {
        get => _buildRetail;
        set { _buildRetail = value; OnPropertyChanged(); }
    }

    public int ProgressPct
    {
        get => _progressPct;
        set { _progressPct = value; OnPropertyChanged(); }
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set { _progressLabel = value; OnPropertyChanged(); }
    }

    public ICommand RunCommand { get; }

    public VitaPatchMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    public async Task RunAsync()
    {
        _totalSw.Restart();

        using (BeginWork())
        {
            try
            {
                _cts = new CancellationTokenSource();

                string decryptedRoot = Path.Combine(OutputPath, "_decrypted");
                Directory.CreateDirectory(decryptedRoot);

                Log("복호화 시작...");
                var preparer = new VitaSourcePreparer();
                var results = preparer.PrepareAll(SourcePath, decryptedRoot);

                foreach (var r in results)
                {
                    if (r.Success)
                        Log($"{r.Item.Category} {r.Item.TitleId}: 복호화 완료");
                    else
                        Log($"{r.Item.Category} {r.Item.TitleId}: 실패 - {r.Error}", LogLevel.Error);
                }

                if (results.Count == 0)
                {
                    Log("app/patch/addcont 폴더를 찾을 수 없습니다.", LogLevel.Error);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(PatchPath) && Directory.Exists(PatchPath))
                {
                    Log("xdelta 패치 매칭 중...");
                    var matches = VitaPatchApplier.Match(decryptedRoot, PatchPath);

                    Log($"매칭된 패치: {matches.Count}개");

                    var progress = new Progress<double>(p => ProgressPct = (int)(p * 100));
                    await VitaPatchApplier.ApplyAllAsync(matches, progress, _cts.Token);

                    Log("패치 적용 완료");
                }

                if (BuildEmu)
                {
                    string emuRoot = Path.Combine(OutputPath, "emu");
                    VitaPatchOutputBuilder.BuildEmuOutput(decryptedRoot, emuRoot);
                    Log($"에뮬용 출력 생성 완료: {emuRoot}");
                }

                if (BuildRetail)
                {
                    string retailRoot = Path.Combine(OutputPath, "retail");
                    VitaPatchOutputBuilder.BuildRetailOutput(decryptedRoot, retailRoot);
                    Log($"실기용 출력 생성 완료: {retailRoot}");
                }

                Log($"전체 완료 ({_totalSw.Elapsed:mm\\:ss})", LogLevel.Ok);
            }
            catch (OperationCanceledException)
            {
                Log("취소되었습니다.", LogLevel.Error);
            }
            catch (Exception ex)
            {
                Log($"오류: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                ProgressPct = 0;
            }
        }
    }

    public void Cancel() => _cts?.Cancel();

    public void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
}