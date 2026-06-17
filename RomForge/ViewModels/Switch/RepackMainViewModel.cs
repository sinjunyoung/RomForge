using Common;
using Common.WPF.ViewModels;
using LibHac.Ns;
using NSW.Core.Enums;
using NSW.Core.Exceptions;
using NSW.HacPack.Models;
using NSW.M1.Core.Services;
using NSW.WPF.UI;
using NSW.WPF.ViewModels;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace NSW.M1.ViewModels
{
    public class RepackMainViewModel : ToolTabViewModel
    {
        private readonly Stopwatch _totalSw = new();
        private CancellationTokenSource? _cts;

        private int _progressPct;
        private string _progressLabel = "대기 중...";
        private string _progressTime = "00:00 경과";
        private string _patchPath = string.Empty;
        private string _outputPath = string.Empty;
        private bool _isWorking;

        public ObservableCollection<LogEntry> LogEntries { get; } = [];


        public event Action<string, LogLevel>? OnLogRequest;
        public event Action<bool, BuildMode>? OnWorkingChanged;

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

        public string ProgressTime
        {
            get => _progressTime;
            set { _progressTime = value; OnPropertyChanged(); }
        }

        public string PatchPath
        {
            get => _patchPath;
            set { _patchPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(PatchHintVisibility)); }
        }

        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OutputHintVisibility)); }
        }

        public bool IsWorking
        {
            get => _isWorking;
            set { _isWorking = value; OnPropertyChanged(); }
        }

        public Visibility PatchHintVisibility => string.IsNullOrEmpty(PatchPath) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

        public record BuildContext(IList<GameFile> GameFiles, GameMetadata? Metadata, ApplicationControlProperty.Language ForcedLanguage);
        public BuildContext? Context { get; set; }

        public ICommand BrowsePatchCommand { get; }
        public ICommand BrowseOutputCommand { get; }

        public RepackMainViewModel()
        {
            OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            BrowsePatchCommand = new RelayCommand(async _ => await BrowsePatch(), null);
            BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput(), null);
        }

        public async Task StartAsync(BuildMode mode)
        {
            if (!TryGetBuildRequest(mode, out var req, out string errorMsg) || req == null)
            {
                MessageBoxHelper.ShowWarning(errorMsg);
                return;
            }

            if (mode != BuildMode.RebuildOnly && Directory.Exists(Path.Combine(req.OutputDir, "unpacked")) &&
                !MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
                return;

            IsWorking = true;
            OnWorkingChanged?.Invoke(true, mode);
            _totalSw.Restart();

            try { await ExecuteBuildAsync(mode, req); }
            finally
            {
                IsWorking = false;
                OnWorkingChanged?.Invoke(false, mode);
                ProgressPct = 0;
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
            Log("🛑 사용자에 의해 취소 요청됨...", LogLevel.Error);
        }

        private async Task ExecuteBuildAsync(BuildMode mode, BuildRequest req)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var progress = new Progress<(int pct, string label)>(p =>
            {
                ProgressPct = p.pct >= 0 ? p.pct : 0;
                ProgressLabel = p.pct >= 0 ? $"{p.label} ({p.pct}%)" : p.label;
                ProgressTime = $"{_totalSw.Elapsed:mm\\:ss} 경과";
            });

            await Task.Run(() =>
            {
                try
                {
                    req.Language = Context?.ForcedLanguage ?? default;
                    req.UserMetadata = Context?.Metadata;
                    req.OverrideKeyGeneration = 1;
                    req.TargetIdOffset = 2;
                    string finalResult = NspBuildService.Run(req, mode, progress, (msg, lvl) => Log(msg, lvl), token);
                    Log($"\n✓ {mode} 완료! 총 소요: {_totalSw.Elapsed:mm\\:ss}", LogLevel.Ok);
                    MessageBoxHelper.ShowInfo($"{mode} 작업이 완료되었습니다!\n{finalResult}");
                    if (Directory.Exists(req.OutputDir)) Process.Start("explorer.exe", $"\"{req.OutputDir}\"");
                }
                catch (OperationCanceledException)
                {
                    Log($"\n🛑 {mode} 작업이 취소되었습니다.", LogLevel.Error);
                    MessageBoxHelper.ShowWarning("작업이 취소되었습니다.");
                }
                catch (UnpackMetadataNotFoundException bex)
                {
                    Log($"\n❌ {mode} 실패: {bex.Message}", LogLevel.Error);
                    MessageBoxHelper.ShowError($"{mode} 작업 중 오류가 발생했습니다:\n{bex.Message}");
                }
                catch (Exception ex)
                {
                    Log($"오류: {ex.Message}", LogLevel.Error);
                    Log(ex.StackTrace ?? "", LogLevel.Error);
                }
                finally { _cts?.Dispose(); _cts = null; }
            }, token);
        }

        private bool TryGetBuildRequest(BuildMode mode, out BuildRequest? req, out string errorMsg)
        {
            req = null;
            errorMsg = string.Empty;
            if (string.IsNullOrEmpty(OutputPath)) { errorMsg = "작업 폴더를 설정하세요."; return false; }
            if (mode == BuildMode.RebuildOnly)
            {
                string unpackedPath = Path.Combine(OutputPath, "unpacked");
                if (!Directory.Exists(unpackedPath)) { errorMsg = "언팩된 데이터가 없습니다."; return false; }
                string tempPath = Path.Combine(OutputPath, "temp");
                if (!Directory.Exists(tempPath)) Directory.CreateDirectory(tempPath);
                req = new BuildRequest("", "", [], PatchPath.Trim(), OutputPath);
                return true;
            }
            var gameFiles = Context?.GameFiles;
            if (gameFiles == null || !gameFiles.Any(f => f.FileType.Contains('B'))) { errorMsg = "원본 파일(BASE)이 리스트에 없습니다."; return false; }
            if (gameFiles.Any(f => f.IsKeyMissing)) { errorMsg = NSW.Core.Properties.Resources.Main_Err_NoKeys; return false; }
            var baseFile = gameFiles.First(f => f.FileType.Contains('B')).FilePath;
            var updateFile = gameFiles.FirstOrDefault(f => f.FileType.Contains('U'))?.FilePath ?? "";
            var dlcFiles = gameFiles.Where(f => f.FileType.Contains('D')).Select(f => f.FilePath).ToList();
            req = new BuildRequest(baseFile, updateFile, dlcFiles, PatchPath.Trim(), OutputPath);
            return true;
        }

        private async Task BrowsePatch()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "한글패치 루트 폴더 선택" };
            if (dlg.ShowDialog() == true) PatchPath = dlg.FolderName;
        }

        private async Task BrowseOutput()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };
            if (dlg.ShowDialog() == true) OutputPath = dlg.FolderName;
        }

        private void Log(string msg, LogLevel level = LogLevel.Info) => OnLogRequest?.Invoke(msg, level);
    }
}