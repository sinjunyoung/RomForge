using Common;
using Common.WPF.ViewModels;
using NSW.WPF.Services;
using RomForge.Core.Models;
using RomForge.Core.Services.Patch;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Vita.Core.Models;
using Vita.Core.Services;

namespace RomForge.ViewModels.Patch;

public class VitaPatchMainViewModel : ToolTabViewModel, IPatchViewModel
{
    private readonly Stopwatch _totalSw = new();
    private CancellationTokenSource? _cts;

    private string? _sourcePath = string.Empty;
    private string? _patchPath = string.Empty;
    private string? _outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "vita");
    private bool _buildEmu = true;
    private bool _buildRetail;
    private bool _mergePatchIntoGame = false;
    private int _progressPct;
    private string _progressLabel = string.Empty;

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            if (_sourcePath != value)
            {
                _sourcePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceLabel));
            }
        }
    }

    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            if (_patchPath != value)
            {
                _patchPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PatchLabel));
            }
        }
    }

    public string SourceLabel => string.IsNullOrEmpty(SourcePath) ? "원본(PKG/ZIP/폴더)을 드래그&드롭하세요" : Path.GetFileName(SourcePath);

    public string PatchLabel => string.IsNullOrEmpty(PatchPath) ? "패치(ZIP/폴더)를 드래그&드롭하세요" : Path.GetFileName(PatchPath);

    public string? OutputPath
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

    public bool MergePatchIntoGame
    {
        get => _mergePatchIntoGame;
        set { _mergePatchIntoGame = value; OnPropertyChanged(); }
    }

    public ICommand RunCommand { get; }

    public VitaPatchMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsLocked && !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(PatchPath) && !string.IsNullOrWhiteSpace(OutputPath) && (BuildEmu || BuildRetail));
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

                string baseName = Path.GetFileNameWithoutExtension(SourcePath!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var progress = new Progress<double>(p => ProgressPct = (int)(p * 100));

                if (BuildEmu)
                {
                    Log("에뮬용 패치 생성 중...");

                    string emuZip = Path.Combine(OutputPath, $"{baseName}_emu.zip");
                    var result = await VitaPatchOnlyBuilder.BuildAsync(SourcePath, PatchPath, emuZip, VitaOutputTarget.Emu, msg => Log(msg), progress, _cts.Token);

                    Log($"에뮬용 완료: 매칭 {result.MatchedCandidates}개 중 {result.PatchedSuccessfully}개 성공 -> {emuZip}", LogLevel.Ok);
                }

                if (BuildRetail)
                {
                    Log("실기용 패치 생성 중...");

                    string retailZip = Path.Combine(OutputPath, $"{baseName}_retail.zip");
                    var result = await VitaPatchOnlyBuilder.BuildAsync(SourcePath, PatchPath, retailZip, VitaOutputTarget.Retail, msg => Log(msg), progress, _cts.Token);

                    Log($"실기용 완료: 매칭 {result.MatchedCandidates}개 중 {result.PatchedSuccessfully}개 성공 -> {retailZip}", LogLevel.Ok);
                }

                Log($"전체 완료 ({_totalSw.Elapsed:mm\\:ss})", LogLevel.Ok);

                OutputPath.OpenFolder();
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

    public void Clear()
    {
        _cts?.Cancel();

        SourcePath = null;
        PatchPath = null;

        ProgressPct = 0;
        ProgressLabel = string.Empty;

        LogEntries.Clear();
    }

    public void Cancel() => _cts?.Cancel();

    public void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
}