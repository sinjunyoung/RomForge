using Common.Models;
using Common.Localization;
using Common.Services;
using LibHac.Ns;
using NSW.Core.Services;
using RomForge.Core;
using RomForge.Core.Services;
using RomForge.Core.Services.Switch;
using RomForge.Core.UI.Command;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace RomForge.ViewModels.Switch
{
    public class ConvertSaturnMainViewModel : ProgressToolTabViewModel
    {
        public ObservableCollection<string> SoundVolumeItems { get; } = ["60%", "80%", "100%", "120%"];

        private byte[]? _coverBytes;
        private readonly Stopwatch _totalSw = new();

        public string GameTitle { get; set { SetProperty(ref field, value); } } = string.Empty;

        public string Publisher { get; set { SetProperty(ref field, value); } } = "SEGA";

        public string GameId { get; set { SetProperty(ref field, value); NspTitleId = Crc32Helper.BuildTitleId(Crc32Helper.Compute(Encoding.Default.GetBytes(value))); OnPropertyChanged(NspTitleId); } } = string.Empty;

        public string GameVersion { get; set { SetProperty(ref field, value); } } = string.Empty;

        public string NspTitleId { get; set { SetProperty(ref field, value); } } = string.Empty;

        public bool WideScreen { get; set { SetProperty(ref field, value); } } = false;

        public bool UseLz4Compression { get; set { SetProperty(ref field, value); } } = true;

        public string SelectedSoundVolume { get; set { SetProperty(ref field, value); } } = "100%";

        public string CuePath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(CueHintVisibility)); _ = ParseSaturnDataAsync(value); } } = string.Empty;

        public string NspPath
        {
            get;
            set
            {
                if (!SetProperty(ref field, value))
                    return;

                OnPropertyChanged(nameof(NspHintVisibility));

                if (string.IsNullOrEmpty(value) || File.Exists(value))
                    AppConfig.Instance.Switch.SaturnNspPath = value;
            }
        } = string.Empty;

        public string WorkPath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(WorkHintVisibility)); } } = string.Empty;

        public string CoverImagePath
        {
            get;
            set
            {
                SetProperty(ref field, value);

                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    SetCover(value.ToImageBytes());
            }
        } = string.Empty;

        public ImageSource? CoverImageSource { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(CoverHintVisibility)); } }

        private void SetCover(byte[]? bytes)
        {
            _coverBytes = bytes;
            CoverImageSource = bytes?.ToBitmapImage();
        }

        public bool IsConverting { get; set { SetProperty(ref field, value); } }

        public override string? HelpPageKey => "switch-nsp-convert";

        public Visibility CueHintVisibility => string.IsNullOrEmpty(CuePath) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NspHintVisibility => string.IsNullOrEmpty(NspPath) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility WorkHintVisibility => string.IsNullOrEmpty(WorkPath) ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CoverHintVisibility => CoverImageSource == null ? Visibility.Visible : Visibility.Collapsed;

        public ICommand BrowseCueCommand { get; }

        public ICommand BrowseNspCommand { get; }

        public ICommand BrowseWorkCommand { get; }

        public ConvertSaturnMainViewModel()
        {
            BrowseCueCommand = new RelayCommand(_ => BrowseCue());
            BrowseNspCommand = new RelayCommand(_ => BrowseNsp());
            BrowseWorkCommand = new RelayCommand(_ => BrowseWork());

            if (AppConfig.Instance.Switch.SaturnNspPath is { Length: > 0 } saved && File.Exists(saved))
                NspPath = saved;
        }

        private void BrowseCue()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = $"{Loc.S.FilterFilesOfType("CUE/CHD")}|*.cue;*.chd" };

            if (dlg.ShowDialog() == true)
                CuePath = dlg.FileName;
        }

        private void BrowseNsp()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = $"{Loc.S.FilterFilesOfType("NSP/NSZ")}|*.nsp;*.nsz" };

            if (Path.GetDirectoryName(NspPath) is { Length: > 0 } directory && Directory.Exists(directory))
                dlg.InitialDirectory = directory;

            if (dlg.ShowDialog() == true)
                NspPath = dlg.FileName;
        }

        private async Task BrowseWork()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.S.DialogSelectWorkFolder };

            if (dlg.ShowDialog() == true)
                WorkPath = dlg.FolderName;
        }

        private static byte[]? TryReadChdFirstSectorHeader(string chdPath)
        {
            try
            {
                using var wrapper = new CHD.Core.Interop.LibChdrWrapper();
                var result = wrapper.Open(chdPath);

                if (result != CHD.Core.Interop.Enums.ChdrError.CHDERR_NONE || !wrapper.Header.HasValue)
                    return null;

                var hunk = wrapper.ReadHunk(0);

                return hunk.Length >= 0x90 ? hunk : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task ParseSaturnDataAsync(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
                return;

            try
            {
                byte[] header;

                if (string.Equals(Path.GetExtension(inputPath), ".chd", StringComparison.OrdinalIgnoreCase))
                    header = await Task.Run(() => TryReadChdFirstSectorHeader(inputPath)) ?? throw new InvalidOperationException(Loc.S.ErrChdHeaderUnreadable);
                else
                {
                    string cuePath = inputPath;
                    string binPath = string.Empty;
                    var lines = await File.ReadAllLinesAsync(cuePath);

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("FILE", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split('"');

                            if (parts.Length > 1)
                            {
                                binPath = Path.Combine(Path.GetDirectoryName(cuePath)!, parts[1]);
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(binPath) || !File.Exists(binPath))
                        return;

                    using var fs = new FileStream(binPath, FileMode.Open, FileAccess.Read);

                    if (fs.Length < 0x90)
                        return;

                    header = new byte[0x90];

                    await fs.ReadExactlyAsync(header.AsMemory(0, 0x90));
                }

                GameId = Encoding.ASCII.GetString(header, 0x30, 10).Trim();
                GameVersion = Encoding.ASCII.GetString(header, 0x3B, 5);
                GameTitle = Encoding.ASCII.GetString(header, 0x70, 32).Trim();

                await TryAutoDownloadCoverAsync(GameId);
            }
            catch
            {
                GameTitle = string.Empty;
                GameId = string.Empty;
                GameVersion = string.Empty;
            }
        }

        private async Task TryAutoDownloadCoverAsync(string gameId)
        {
            var bytes = await SaturnCoverArtFetcher.TryDownloadCoverPngAsync(gameId);

            SetCover(bytes);
        }

        public async Task ConvertAsync()
        {
            if (!ValidateInputs(out string err))
            {
                AppendLog(err, LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                return;
            }

            IsConverting = true;

            _totalSw.Restart();

            using (BeginWork())
            {
                CancellationTokenSource = new CancellationTokenSource();

                try
                {
                    await RunConversionProcess(CancellationTokenSource.Token);
                    await SoundPlayer.PlayCompleteAsync();
                    AppendLog(Loc.S.LogDoneTotalTime(_totalSw.Elapsed.ToString(@"mm\:ss")), LogLevel.Ok);
                }
                catch (OperationCanceledException)
                {
                    AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);
                    await SoundPlayer.PlayCancelAsync();
                }
                catch (Exception ex)
                {
                    AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                    await SoundPlayer.PlayErrorAsync();
                    MainViewModel.SetTaskbarProgress(100, System.Windows.Shell.TaskbarItemProgressState.Error);
                }
                finally
                {
                    IsConverting = false;

                    CleanupTask();
                }
            }
        }

        private async Task RunConversionProcess(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                if (!Directory.Exists(WorkPath))
                    WorkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

                var progress = new Progress<ProgressInfo>(p =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (p.Percent >= 0)
                        {
                            ProgressPct = p.Percent;
                            ProgressPercent = ProgressPct.ToString("0.00") + "%";
                            ProgressLabel = $"{p.Label} ({p.Percent}%)";
                        }
                        else
                        {
                            ProgressPercent = string.Empty;

                            if (!string.IsNullOrEmpty(p.Label))
                                ProgressLabel = p.Label;
                        }

                        if (!string.IsNullOrEmpty(p.Speed))
                            ProgressSpeed = p.Speed;

                        ProgressTime = string.IsNullOrEmpty(p.TimeInfo) ? Loc.S.ProgressElapsed(_totalSw.Elapsed.ToString(@"mm\:ss")) : p.TimeInfo;
                    });
                });

                void Log(string msg) => Application.Current?.Dispatcher.Invoke(() => AppendLog(msg));

                double volumeMultiplier = 1.0;

                if (!string.IsNullOrEmpty(SelectedSoundVolume) && double.TryParse(SelectedSoundVolume.TrimEnd('%'), out double parsedVal))
                    volumeMultiplier = parsedVal / 100.0;

                Log(Loc.S.LogStreamingConvertStart);

                var request = new NSW.Core.Models.BuildRequest(NspPath, string.Empty, [], string.Empty, WorkPath)
                {
                    Language = ApplicationControlProperty.Language.None,
                    OverrideTitleId = ulong.Parse(NspTitleId, System.Globalization.NumberStyles.HexNumber),
                    CompressRomfs = UseLz4Compression,
                    StreamRomfs = true,
                    OnUnpacked = unpackResult => InjectSaturnDisc(unpackResult, volumeMultiplier, Log)
                };

                string finalNspPath = await NspBuildService.Run(request, BuildMode.FullProcess, AppConfig.Instance.Switch.Lz4CompressLevel, progress, (msg, lvl) => Log(msg), token);
                string finalRenamedPath = RenameOutputFile(finalNspPath, GameTitle, NspTitleId);

            Application.Current?.Dispatcher.Invoke(() => Path.GetDirectoryName(finalRenamedPath)!.OpenFolder());
            }, token);
        }

        private NSW.HacPack.Models.GameMetadata? InjectSaturnDisc(NSW.Core.Models.UnpackResult unpackResult, double volumeMultiplier, Action<string> log)
        {
            if (unpackResult.VirtualRomfsPrograms.Count == 0)
                throw new DirectoryNotFoundException(Loc.S.ErrProgramRomfsNotFound);

            byte idOffset = unpackResult.VirtualRomfsPrograms.Keys.Min();

            log(string.Equals(Path.GetExtension(CuePath), ".chd", StringComparison.OrdinalIgnoreCase) ? Loc.S.LogLinkingChdStreaming : Loc.S.LogLinkingBinCueStreaming);
            SaturnRomfsInjector.Inject(unpackResult.VirtualRomfsPrograms[idOffset], CuePath, WideScreen, volumeMultiplier);

            if (!unpackResult.VirtualParts.TryGetValue(idOffset, out var parts) || parts.Control == null)
                return null;

            var metadata = MetadataService.GetGameMetadataFromControl(parts.Control);
            var koLang = metadata?.Languages.FirstOrDefault(l => l.Language == ApplicationControlProperty.Language.Korean) ?? metadata?.Languages.FirstOrDefault();

            if (koLang != null)
            {
                koLang.TitleName = GameTitle;
                koLang.Publisher = Publisher;
                koLang.Flag = true;

                if (_coverBytes != null)
                    koLang.LogoData = _coverBytes;
            }

            return metadata;
        }

        private static string RenameOutputFile(string currentPath, string gameTitle, string titleIdStr)
        {
            if (!File.Exists(currentPath))
                return currentPath;

            string safeTitle = gameTitle;

            foreach (char c in Path.GetInvalidFileNameChars())
                safeTitle = safeTitle.Replace(c, '_');

            string dir = Path.GetDirectoryName(currentPath)!;
            string newFileName = $"{safeTitle} [{titleIdStr.ToUpperInvariant()}]_Saturn.nsp";
            string newPath = Utils.GetUniqueFilePath(Path.Combine(dir, newFileName));

            File.Move(currentPath, newPath);

            return newPath;
        }

        private bool ValidateInputs(out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrEmpty(NspPath) || !File.Exists(NspPath))
            {
                errorMsg = Loc.S.ErrSelectSourceSwitchFile;
                return false;
            }

            if (string.IsNullOrEmpty(CuePath) || !File.Exists(CuePath))
            {
                errorMsg = Loc.S.ErrSelectCueChd;
                return false;
            }

            string ext = Path.GetExtension(CuePath).ToLowerInvariant();

            if (ext != ".cue" && ext != ".chd")
            {
                errorMsg = Loc.S.ErrOnlyCueChdSupported;
                return false;
            }

            if (string.IsNullOrEmpty(GameTitle))
            {
                errorMsg = Loc.S.ErrEnterGameName;
                return false;
            }

            if (this.GameId == "T-1315G")
            {
                errorMsg = Loc.S.ErrSaturnGameUnsupported;
                return false;
            }

            return true;
        }
    }
}