using Common;
using Common.WPF.ViewModels;
using LibHac.Ns;
using NSW.Core.Enums;
using NSW.M1.Core.Services;
using NSW.WPF.Services;
using RomForge.Core.Models;
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
    public class ConvertSaturnMainViewModel : ToolTabViewModel
    {
        private string _gameTitle = string.Empty;
        private string _publisher = "SEGA";
        private string _gameId = string.Empty;
        private string _gameVersion = string.Empty;

        private bool _wideScreen = false;
        private string _selectedSoundVolume = "100%";

        public ObservableCollection<string> SoundVolumeItems { get; } =
            ["60%", "80%", "100%", "120%"];

        private string _cuePath = string.Empty;
        private string _nspPath = string.Empty;
        private string _workPath = string.Empty;
        private string _coverImagePath = string.Empty;
        private ImageSource? _coverImageSource;
        private byte[]? _coverBytes;

        private bool _isConverting;
        private string _progressLabel = "대기 중...";
        private string _progressPercent = "0%";
        private string _progressSpeed = string.Empty;
        private string _progressTime = "00:00 경과";
        private double _progressPct;

        private readonly Stopwatch _totalSw = new();

        private CancellationTokenSource? _cts;

        public ObservableCollection<LogEntry> LogEntries { get; } = [];


        public string GameTitle { get => _gameTitle; set { _gameTitle = value; OnPropertyChanged(); } }

        public string Publisher { get => _publisher; set { _publisher = value; OnPropertyChanged(); } }

        public string GameId { get => _gameId; set { _gameId = value; OnPropertyChanged(); } }

        public string GameVersion { get => _gameVersion; set { _gameVersion = value; OnPropertyChanged(); } }

        public bool WideScreen { get => _wideScreen; set { _wideScreen = value; OnPropertyChanged(); } }

        public string SelectedSoundVolume
        {
            get => _selectedSoundVolume;
            set { _selectedSoundVolume = value; OnPropertyChanged(); }
        }

        public string CuePath
        {
            get => _cuePath;
            set { _cuePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CueHintVisibility)); _ = ParseSaturnDataAsync(value); }
        }

        public string NspPath
        {
            get => _nspPath;
            set { _nspPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(NspHintVisibility)); }
        }

        public string WorkPath
        {
            get => _workPath;
            set { _workPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(WorkHintVisibility)); }
        }

        public string CoverImagePath
        {
            get => _coverImagePath;
            set
            {
                _coverImagePath = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    SetCover(value.ToImageBytes());
            }
        }

        public ImageSource? CoverImageSource
        {
            get => _coverImageSource;
            set { _coverImageSource = value; OnPropertyChanged(); OnPropertyChanged(nameof(CoverHintVisibility)); }
        }

        private void SetCover(byte[] bytes)
        {
            _coverBytes = bytes;
            CoverImageSource = bytes.ToBitmapImage();
        }

        public bool IsConverting { get => _isConverting; set { _isConverting = value; OnPropertyChanged(); } }

        public string ProgressLabel { get => _progressLabel; set { _progressLabel = value; OnPropertyChanged(); } }

        public string ProgressPercent { get => _progressPercent; set { _progressPercent = value; OnPropertyChanged(); } }

        public string ProgressSpeed { get => _progressSpeed; set { _progressSpeed = value; OnPropertyChanged(); } }

        public string ProgressTime { get => _progressTime; set { _progressTime = value; OnPropertyChanged(); } }

        public double ProgressPct { get => _progressPct; set { _progressPct = value; OnPropertyChanged(); } }

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
        }

        private void BrowseCue()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "CUE 파일|*.cue" };
            if (dlg.ShowDialog() == true) CuePath = dlg.FileName;
        }

        private void BrowseNsp()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "NSP/NSZ 파일|*.nsp;*.nsz" };
            if (dlg.ShowDialog() == true) NspPath = dlg.FileName;
        }

        private async Task BrowseWork()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "작업 폴더 선택" };

            if (dlg.ShowDialog() == true)
                WorkPath = dlg.FolderName;
        }

        private async Task ParseSaturnDataAsync(string cuePath)
        {
            if (string.IsNullOrEmpty(cuePath) || !File.Exists(cuePath))
                return;

            try
            {
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

                fs.Seek(0x30, SeekOrigin.Begin);

                var buffer = new byte[10];

                await fs.ReadAsync(buffer.AsMemory(0, 10));

                GameId = Encoding.ASCII.GetString(buffer).Trim();

                buffer = new byte[5];

                fs.Seek(0x3B, SeekOrigin.Begin);

                await fs.ReadAsync(buffer.AsMemory(0, 5));

                GameVersion = Encoding.ASCII.GetString(buffer);

                fs.Seek(0x70, SeekOrigin.Begin);

                var titleBuffer = new byte[32];

                await fs.ReadAsync(titleBuffer.AsMemory(0, 32));

                GameTitle = Encoding.ASCII.GetString(titleBuffer).Trim();

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

            if (bytes == null)
                return;

            SetCover(bytes);
        }

        public async Task ConvertAsync()
        {
            if (!ValidateInputs(out string err))
            {
                Log(err, LogLevel.Error);
                return;
            }

            IsConverting = true;
            _totalSw.Restart();

            using (BeginWork())
            {
                _cts = new CancellationTokenSource();
                try
                {
                    await RunConversionProcess(_cts.Token);
                    Log($"완료! 총 소요: {_totalSw.Elapsed:mm\\:ss}", LogLevel.Ok);
                }
                catch (OperationCanceledException)
                {
                    Log($"작업이 취소되었습니다.", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    Log($"오류: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    _cts?.Dispose();
                    _cts = null;
                    IsConverting = false;
                    ProgressPct = 0;
                }
            }
        }

        private async Task RunConversionProcess(CancellationToken token)
        {
            if (!Directory.Exists(WorkPath))
                WorkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

            string unpackedDir = Path.Combine(WorkPath, "unpacked");
            string romfsDir = Path.Combine(unpackedDir, "romfs");

            try
            {
                var progress = new Progress<(int pct, string label)>(p =>
                {
                    ProgressPct = p.pct >= 0 ? p.pct : 0;
                    ProgressPercent = ProgressPct.ToString("0.00") + "%";
                    ProgressLabel = p.pct >= 0 ? $"{p.label} ({p.pct}%)" : p.label;
                    ProgressTime = $"{_totalSw.Elapsed:mm\\:ss} 경과";
                });

                Log("원본 NSP 언팩 중...");
                var unpackReq = new BuildRequest(NspPath, string.Empty, [], string.Empty, WorkPath);
                await NspBuildService.Run(unpackReq, BuildMode.UnpackOnly, progress, (msg, lvl) => Log(msg), token);

                token.ThrowIfCancellationRequested();

                string? existingCuePath = (Directory.Exists(romfsDir)
                    ? Directory.GetFiles(romfsDir, "*.cue", SearchOption.AllDirectories).FirstOrDefault()
                    : null) ?? throw new FileNotFoundException($"romfs 안에서 cue 파일을 찾을 수 없습니다.");

                string targetDir = Path.GetDirectoryName(existingCuePath)!;
                var oldBins = CHD.Core.Services.ConversionSource.ParseBinsFromCue(existingCuePath);

                foreach (var bin in oldBins)
                {
                    if (File.Exists(bin)) File.Delete(bin);
                }
                File.Delete(existingCuePath);

                var newBins = CHD.Core.Services.ConversionSource.ParseBinsFromCue(CuePath);
                if (newBins.Count == 0) throw new FileNotFoundException("입력한 CUE에서 참조하는 BIN 파일을 찾을 수 없습니다.");

                foreach (var bin in newBins)
                {
                    if (!File.Exists(bin)) throw new FileNotFoundException($"BIN 파일이 존재하지 않습니다: {bin}");
                    await CopyFileAsync(bin, Path.Combine(targetDir, Path.GetFileName(bin)), token);
                }

                string cueFileName = Path.GetFileName(existingCuePath);
                IniHandler ini = new($"{targetDir}\\{Path.GetFileNameWithoutExtension(cueFileName)}_Switch.ini");
                await ini.LoadAsync();
                ini.SetValue("Screen", "WideScreen", WideScreen ? "1" : "0");

                double volumeMultiplier = 1.0;

                if (!string.IsNullOrEmpty(SelectedSoundVolume) && double.TryParse(SelectedSoundVolume.TrimEnd('%'), out double parsedVal))
                    volumeMultiplier = parsedVal / 100.0;

                ini.SetValue("Sound", "Volume", volumeMultiplier.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                await ini.SaveAsync();
                await CopyFileAsync(CuePath, Path.Combine(targetDir, cueFileName), token);

                uint crc = Crc32Helper.ComputeFile(newBins[0]);
                string titleIdStr = Crc32Helper.BuildTitleId(crc);
                var metadata = MetadataService.GetGameMetadataFromUnpacked(unpackedDir);
                var koLang = metadata?.Languages.FirstOrDefault(l => l.Language == ApplicationControlProperty.Language.Korean)
                             ?? metadata?.Languages.First();

                if (koLang != null)
                {
                    koLang.TitleName = GameTitle;
                    koLang.Publisher = Publisher;
                    koLang.Flag = true;
                    if (_coverBytes != null)
                        koLang.LogoData = _coverBytes;
                }

                token.ThrowIfCancellationRequested();

                Log("리팩 중...");
                var rebuildReq = new BuildRequest(string.Empty, string.Empty, [], string.Empty, WorkPath)
                {
                    UserMetadata = metadata,
                    Language = ApplicationControlProperty.Language.None,
                    OverrideTitleId = ulong.Parse(titleIdStr, System.Globalization.NumberStyles.HexNumber)
                };

                string finalNspPath = await NspBuildService.Run(rebuildReq, BuildMode.RebuildOnly, progress, (msg, lvl) => Log(msg), token);
                string finalRenamedPath = RenameOutputFile(finalNspPath, GameTitle, titleIdStr);

                Path.GetDirectoryName(finalRenamedPath)!.OpenFolder();
            }
            finally
            {
                Directory.Delete(unpackedDir, true);
            }
        }

        private static async Task CopyFileAsync(string sourcePath, string destPath, CancellationToken ct)
        {
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
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

        public void Cancel() => _cts?.Cancel();

        private bool ValidateInputs(out string errorMsg)
        {
            errorMsg = string.Empty;

            if (string.IsNullOrEmpty(NspPath) || !File.Exists(NspPath))
            {
                errorMsg = "원본 스위치 파일(NSP/NSZ)을 선택하세요.";
                return false;
            }

            if (string.IsNullOrEmpty(CuePath) || !File.Exists(CuePath))
            {
                errorMsg = "CUE 파일을 선택하세요.";
                return false;
            }

            if (string.IsNullOrEmpty(GameTitle))
            {
                errorMsg = "게임명을 입력하세요.";
                return false;
            }

            if (this.GameId == "T-1315G")
            {
                errorMsg = "통곡 그리고...는 현재 버전에서는 지원하지 않습니다.";
                return false;
            }

            return true;
        }

        private void Log(string msg, LogLevel level = LogLevel.Info) => Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    }
}