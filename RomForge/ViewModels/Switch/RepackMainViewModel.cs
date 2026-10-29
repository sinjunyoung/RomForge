using Common;
using LibHac.Ns;
using NSW.Core.Exceptions;
using NSW.Core.Models;
using NSW.Core.Services;
using NSW.HacPack.Models;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.UI.Command;
using RomForge.Core.ViewModels.Switch;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Switch
{
    public class RepackMainViewModel : ProgressToolTabViewModel
    {
        private readonly Stopwatch _totalSw = new();

        private BuildMode? _currentMode;

        public bool IsIndeterminate {  get; set { SetProperty(ref field, value); } } = false;

        public string OutputPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(OutputHintVisibility)); AppConfig.Instance.OutputFolders.SwitchRepackOutputPath = value; } } = string.Empty;

        public bool IsUnpackRunning => IsLocked && _currentMode == BuildMode.UnpackOnly;

        public bool IsRebuildRunning => IsLocked && _currentMode == BuildMode.RebuildOnly;

        public bool IsFullRunning => IsLocked && _currentMode == BuildMode.FullProcess;

        public bool UnpackEnabled => !IsLocked || _currentMode == BuildMode.UnpackOnly;

        public bool RebuildEnabled => !IsLocked || _currentMode == BuildMode.RebuildOnly;

        public bool StartEnabled => !IsLocked || _currentMode == BuildMode.FullProcess;

        public Visibility OutputHintVisibility => string.IsNullOrEmpty(OutputPath) ? Visibility.Visible : Visibility.Collapsed;

        public record BuildContext(IList<GameFile> GameFiles, GameMetadata? Metadata, ApplicationControlProperty.Language ForcedLanguage, byte? TargetIdOffset, ulong? OverrideTitleId);

        public BuildContext? Context { get; set; }

        public ICommand BrowseOutputCommand { get; }

        public RepackMainViewModel()
        {
            OutputPath = string.IsNullOrWhiteSpace(AppConfig.Instance.OutputFolders.SwitchRepackOutputPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output") : AppConfig.Instance.OutputFolders.SwitchRepackOutputPath;
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
                AppendLog(errorMsg, LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
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
                    CleanupTask();
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

        private async Task ExecuteBuildAsync(BuildMode mode, BuildRequest req)
        {
            CancellationTokenSource = new CancellationTokenSource();

            var token = CancellationTokenSource.Token;
            var progress = new Progress<ProgressInfo>(p =>
            {
                bool isIndeterminate = p.Percent < 0;

                ProgressPct = isIndeterminate ? 0 : p.Percent;
                IsIndeterminate = isIndeterminate;
                ProgressLabel = p.Label;
                ProgressPercent = isIndeterminate ? string.Empty : $"{p.Percent}%";
                ProgressSpeed = p.Speed;
                ProgressTime = isIndeterminate ? string.Empty : (string.IsNullOrEmpty(p.TimeInfo) ? $"{_totalSw.Elapsed:mm\\:ss} 경과" : p.TimeInfo);
            });

            await Task.Run(async () =>
            {
                try
                {
                    req.Language = Context?.ForcedLanguage ?? default;
                    req.UserMetadata = Context?.Metadata;
                    req.OverrideKeyGeneration = 1;

                    if (Context?.TargetIdOffset.HasValue == true)
                        req.TargetIdOffset = Context?.TargetIdOffset;

                    if (Context?.OverrideTitleId.HasValue == true)
                        req.OverrideTitleId = Context?.OverrideTitleId;

                    string finalResult = await NspBuildService.Run(req, mode, AppConfig.Instance.Switch.Lz4CompressLevel, progress, (msg, lvl) => AppendLog(msg, lvl), token);

                    AppendLog($"완료! 총 소요: {_totalSw.Elapsed:mm\\:ss}", LogLevel.Ok);
                    await SoundPlayer.PlayCompleteAsync();
                    req.OutputDir?.OpenFolder();
                }
                catch (OperationCanceledException)
                {
                    AppendLog($"작업이 취소되었습니다.", LogLevel.Error);

                    IsIndeterminate = false;

                    await SoundPlayer.PlayCancelAsync();
                    MainViewModel.SetTaskbarProgress(0);

                    try
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();

                        new WorkDirs(req.OutputDir).Cleanup(keepUnpacked: mode == BuildMode.RebuildOnly);
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.WriteLine($"취소 후 작업 디렉터리 정리 실패: {cleanupEx.Message}");
                    }
                }
                catch (UnpackMetadataNotFoundException bex)
                {
                    AppendLog($"실패: {bex.Message}", LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                    MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                }
                catch (Exception ex)
                {
                    AppendLog($"오류: {ex.Message}", LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                    MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                }
                finally
                {
                    CleanupTask();
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

                req = new BuildRequest(string.Empty, string.Empty, [], string.Empty, OutputPath)
                {
                    CompressRomfs = Context?.GameFiles.FirstOrDefault(f => f.FileType.Contains('B'))?.GetLz4Override(rebuildOnly: true)
                };

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
                errorMsg = NSW.Properties.Resources.Main_Err_NoKeys;
                return false;
            }

            var baseEntry = gameFiles.First(f => f.FileType.Contains('B'));
            var updateEntry = gameFiles.FirstOrDefault(f => f.FileType.Contains('U'));
            var dlcEntries = gameFiles.Where(f => f.FileType.Contains('D')).ToList();
            var mainPatchSource = !string.IsNullOrEmpty(baseEntry.PatchPath) ? baseEntry : updateEntry;
            string mainPatch = (mainPatchSource?.PatchPath ?? string.Empty).Trim();
            string? mainPatchPassword = mainPatchSource?.PatchPassword;
            var dlcPatchDirs = dlcEntries
                .Where(f => !string.IsNullOrEmpty(f.PatchPath) && !string.IsNullOrEmpty(f.TitleID))
                .ToDictionary(f => f.TitleID!, f => f.PatchPath!.Trim(), StringComparer.OrdinalIgnoreCase);
            var dlcPatchPasswords = dlcEntries
                .Where(f => !string.IsNullOrEmpty(f.PatchPath) && !string.IsNullOrEmpty(f.TitleID) && !string.IsNullOrEmpty(f.PatchPassword))
                .ToDictionary(f => f.TitleID!, f => f.PatchPassword!, StringComparer.OrdinalIgnoreCase);
            bool hasUnresolvedRow = gameFiles.Any(f => f.FileType.Contains('D') && (f.FileType.Contains('B') || f.FileType.Contains('U')));
            Dictionary<string, HashSet<string>>? dlcTitleIds = hasUnresolvedRow
                ? null
                : dlcEntries
                    .Where(f => !string.IsNullOrEmpty(f.TitleID))
                    .GroupBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.TitleID!).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            req = new BuildRequest(baseEntry.FilePath, updateEntry?.FilePath ?? string.Empty, [.. dlcEntries.Select(f => f.FilePath)], mainPatch, OutputPath)
            {
                DlcTitleIdsByFile = dlcTitleIds,
                DlcPatchDirs = dlcPatchDirs,
                PatchPassword = mainPatchPassword,
                DlcPatchPasswords = dlcPatchPasswords,
                CompressRomfs = baseEntry.GetLz4Override(rebuildOnly: false)
            };

            return true;
        }

        private async Task BrowseOutput()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

            if (dlg.ShowDialog() == true)
                OutputPath = dlg.FolderName;
        }
    }
}