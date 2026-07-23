using Common;
using Common.WPF.ViewModels;
using LibHac.Ns;
using NSW.Core.Enums;
using NSW.Core.Exceptions;
using NSW.HacPack.Models;
using NSW.M1.Core.Services;
using NSW.WPF.Services;
using NSW.WPF.UI;
using NSW.WPF.ViewModels;
using RomForge.Core.UI.Command;
using RomForge.Core.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Switch
{
    public class RepackMainViewModel : ToolTabViewModel
    {
        private readonly Stopwatch _totalSw = new();
        private CancellationTokenSource? _cts;

        private int _progressPct;
        private string _progressLabel = "대기 중...";
        private string _progressTime = "00:00 경과";
        private string _outputPath = string.Empty;
        private BuildMode? _currentMode;        
        
        public ObservableCollection<LogEntry> LogEntries { get; } = [];

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

        public string OutputPath
        {
            get => _outputPath;
            set { _outputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(OutputHintVisibility)); }
        }

        public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

        public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

        public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

        public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

        public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

        public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

        public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

        public record BuildContext(IList<GameFile> GameFiles, GameMetadata? Metadata, ApplicationControlProperty.Language ForcedLanguage, byte? TargetIdOffset);

        public BuildContext? Context { get; set; }

        public ICommand BrowseOutputCommand { get; }

        public RepackMainViewModel()
        {
            OutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            BrowseOutputCommand = new RelayCommand(async _ => await BrowseOutput());

            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsLocked))
                    NotifyButtonStates();
            };
        }

        public async Task StartAsync(BuildMode mode)
        {
            if (!TryGetBuildRequest(mode, out var req, out string errorMsg) || req == null)
            {
                Log(errorMsg, LogLevel.Error);
                return;
            }

            bool isRebuild = (mode == BuildMode.RebuildOnly);
            string unpackedPath = Path.Combine(req.OutputDir, "unpacked");

            if (!isRebuild && Directory.Exists(unpackedPath))
            {
                if (!MessageBoxHelper.ShowQuestion("기존 언팩 데이터를 삭제하고 새로 진행할까요?"))
                    return;
            }

            _totalSw.Restart();
            _currentMode = mode;
            NotifyButtonStates();

            using (BeginWork())
            {
                try
                {
                    await ExecuteBuildAsync(mode, req);
                }
                finally
                {
                    ProgressPct = 0;
                }
            }

            _currentMode = null;
            NotifyButtonStates();
        }

        private void NotifyButtonStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsUnpackRunning));
                OnPropertyChanged(nameof(IsRebuildRunning));
                OnPropertyChanged(nameof(IsFullRunning));
                OnPropertyChanged(nameof(UnpackEnabled));
                OnPropertyChanged(nameof(RebuildEnabled));
                OnPropertyChanged(nameof(StartEnabled));
            });
        }

        public void Cancel()
        {
            _cts?.Cancel();
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

            await Task.Run(async () =>
            {
                try
                {
                    req.Language = Context?.ForcedLanguage ?? default;
                    req.UserMetadata = Context?.Metadata;
                    req.OverrideKeyGeneration = 1;

                    if(Context?.TargetIdOffset.HasValue == true)
                        req.TargetIdOffset = Context?.TargetIdOffset;

                    string finalResult = await NspBuildService.Run(req, mode, progress, (msg, lvl) => Log(msg, lvl), token);

                    Log($"완료! 총 소요: {_totalSw.Elapsed:mm\\:ss}", LogLevel.Ok);

                    req.OutputDir?.OpenFolder();
                }
                catch (OperationCanceledException)
                {
                    Log($"작업이 취소되었습니다.", LogLevel.Error);
                }
                catch (UnpackMetadataNotFoundException bex)
                {
                    Log($"실패: {bex.Message}", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    Log($"오류: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    _cts?.Dispose();
                    _cts = null;
                }
            }, token);
        }

        private bool TryGetBuildRequest(BuildMode mode, out BuildRequest? req, out string errorMsg)
        {
            req = null;
            errorMsg = string.Empty;

            if (string.IsNullOrEmpty(OutputPath))
            {
                errorMsg = "작업 폴더를 설정하세요.";
                return false;
            }

            if (mode == BuildMode.RebuildOnly)
            {
                string unpackedPath = Path.Combine(OutputPath, "unpacked");

                if (!Directory.Exists(unpackedPath))
                {
                    errorMsg = "언팩된 데이터가 없습니다.";
                    return false;
                }

                string tempPath = Path.Combine(OutputPath, "temp");
                if (!Directory.Exists(tempPath))
                    Directory.CreateDirectory(tempPath);

                req = new BuildRequest(string.Empty, string.Empty, [], string.Empty, OutputPath);
                return true;
            }

            var gameFiles = Context?.GameFiles;

            if (gameFiles == null || !gameFiles.Any(f => f.FileType.Contains('B')))
            {
                errorMsg = "원본 파일(BASE)이 리스트에 없습니다.";
                return false;
            }

            if (gameFiles.Any(f => f.IsKeyMissing))
            {
                errorMsg = NSW.Core.Properties.Resources.Main_Err_NoKeys;
                return false;
            }

            var baseEntry = gameFiles.First(f => f.FileType.Contains('B'));
            var updateEntry = gameFiles.FirstOrDefault(f => f.FileType.Contains('U'));
            var dlcEntries = gameFiles.Where(f => f.FileType.Contains('D')).ToList();

            string mainPatch = (baseEntry.PatchPath ?? updateEntry?.PatchPath ?? string.Empty).Trim();

            var dlcPatchDirs = dlcEntries
                .Where(f => !string.IsNullOrEmpty(f.PatchPath) && !string.IsNullOrEmpty(f.TitleID))
                .ToDictionary(f => f.TitleID!, f => f.PatchPath!.Trim(), StringComparer.OrdinalIgnoreCase);

            req = new BuildRequest(baseEntry.FilePath, updateEntry?.FilePath ?? string.Empty, [.. dlcEntries.Select(f => f.FilePath)], mainPatch, OutputPath)
            {
                DlcPatchDirs = dlcPatchDirs
            };

            return true;
        }

        private async Task BrowseOutput()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

            if (dlg.ShowDialog() == true) 
                OutputPath = dlg.FolderName;
        }

        private void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }
}