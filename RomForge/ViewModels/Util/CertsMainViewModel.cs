using Common;
using Common.WPF.ViewModels;
using RomForge.Core.UI.Command;
using RomForge.Core.Models;
using RomForge.Core.Models._3DS;
using RomForge.Core.Models.Util;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace RomForge.ViewModels.Util;

public class CertsMainViewModel : ToolTabViewModel
{
    private const long CertOffset = 0x2040;
    private const int CertSize = 0xA00;
    private const string OutputFileName = "certs.bin";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cia"
    };

    #region Fields

    private bool _isExtracting;
    private CancellationTokenSource _cts = new();
    private CertsFileItem? _selectedFile;
    private TitleViewModel? _romInfo;

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    #endregion

    #region Properties

    public CertsFileItem? SelectedFile
    {
        get => _selectedFile;
        set
        {
            _selectedFile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HintVisibility));
            OnPropertyChanged(nameof(FileInfoVisibility));
            OnPropertyChanged(nameof(ProgressStatus));
            OnPropertyChanged(nameof(ProgressText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public TitleViewModel? RomInfo
    {
        get => _romInfo;
        set { _romInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(RomInfoVisibility)); }
    }

    public bool IsExtracting
    {
        get => _isExtracting;
        set { _isExtracting = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => SelectedFile == null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FileInfoVisibility => SelectedFile != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RomInfoVisibility => RomInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public static string OutputPath => Path.Combine(AppContext.BaseDirectory, OutputFileName);

    public string ProgressStatus => SelectedFile == null ? "파일을 드래그&드롭하거나 선택하세요" : $"출력: {OutputPath}";

    public string ProgressText => SelectedFile == null ? "" : $"{SelectedFile.Progress}%";

    #endregion

    #region Commands

    public ICommand RunCommand { get; }
    public ICommand ClearFileCommand { get; }

    #endregion

    #region Constructor

    public CertsMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsExtracting && SelectedFile != null);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsExtracting);
        ClearFileCommand = new RelayCommand(_ => ClearFile(), _ => !IsExtracting);
    }

    #endregion

    #region Public Methods

    public void SetFile(string path)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(path)))
        {
            AppendLog($"지원하지 않는 파일 형식입니다: {Path.GetExtension(path)}", LogLevel.Error);
            return;
        }

        if (!File.Exists(path))
        {
            AppendLog($"파일을 찾을 수 없습니다: {path}", LogLevel.Error);
            return;
        }

        SelectedFile = new CertsFileItem(path);
        RomInfo = null;
        AppendLog($"파일 로드됨: {SelectedFile.FileName}");
        _ = LoadRomInfoAsync(path);
    }

    public void ClearFile()
    {
        SelectedFile = null;
        ClearLog();
    }

    public static string GetFileDialogFilter() => "CIA 파일|*.cia|모든 파일|*.*";

    #endregion

    #region Private Methods

    private async Task LoadRomInfoAsync(string path)
    {
        try
        {
            var result = await Task.Run(() => Core.Services._3DS.Util.ParseFile(path));


            BitmapSource? icon = null;

            if (result?.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result?.IconPixels, 48 * 4);
                bitmap.Freeze();
                icon = bitmap;
            }

            RomInfo = new TitleViewModel
            {
                Title = result?.Title,
                FilePath = result?.FilePath,
                ShortDescription = result?.ShortDescription,
                Publisher = result?.Publisher,
                ProductCode = result?.ProductCode,
                Crypto = result!.Crypto,
                Icon = icon
            };
        }
        catch (Exception ex)
        {
            AppendLog($"게임 정보 로드 실패: {ex.Message}", LogLevel.Error);
        }
    }

    private async Task RunAsync()
    {
        if (SelectedFile == null)
            return;

        IsExtracting = true;
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            try
            {
                SelectedFile.Status = "추출중";
                SelectedFile.Progress = 0;
                MainViewModel.SetTaskbarProgress(0);

                AppendLog($"certs.bin 추출 시작: {SelectedFile.FileName}", LogLevel.Highlight);
                AppendLog($"오프셋: 0x{CertOffset:X4} / 크기: 0x{CertSize:X3} bytes");
                AppendLog($"출력 경로: {OutputPath}");

                bool success = await Task.Run(() => ExtractCerts(SelectedFile, _cts.Token));

                if (success)
                {
                    SelectedFile.Progress = 100;
                    MainViewModel.SetTaskbarProgress(100);
                    SelectedFile.Status = "완료";

                    OnPropertyChanged(nameof(ProgressText));
                    AppendLog($"추출 완료: {OutputFileName}", LogLevel.Highlight);
                }
                else
                {
                    SelectedFile.Progress = 0;
                    MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
                    if (SelectedFile.Status == "추출중")
                        SelectedFile.Status = "실패";
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                MainViewModel.SetTaskbarProgress(0);

                if (SelectedFile.Status == "추출중")
                {
                    SelectedFile.Status = "취소";
                    SelectedFile.Progress = 0;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"오류 발생: {ex.Message}", LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);

                SelectedFile.Status = "실패";
            }
            finally
            {
                IsExtracting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool ExtractCerts(CertsFileItem item, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (fs.Length < CertOffset + CertSize)
            {
                AppendLog($"[실패] 파일 크기가 너무 작습니다. (필요: 0x{CertOffset + CertSize:X}바이트, 실제: 0x{fs.Length:X}바이트)", LogLevel.Error);
                item.Status = "실패";
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);

                return false;
            }

            fs.Seek(CertOffset, SeekOrigin.Begin);

            byte[] buffer = new byte[CertSize];
            int totalRead = 0;

            while (totalRead < CertSize)
            {
                ct.ThrowIfCancellationRequested();

                int read = fs.Read(buffer, totalRead, CertSize - totalRead);

                if (read == 0)
                    break;

                totalRead += read;
            }

            if (totalRead != CertSize)
            {
                AppendLog($"[실패] 읽기 불완전: 예상 0x{CertSize:X}바이트, 실제 0x{totalRead:X}바이트", LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
                item.Status = "실패";                

                return false;
            }

            ct.ThrowIfCancellationRequested();

            File.WriteAllBytes(OutputPath, buffer);
            AppendLog($"[성공] {totalRead:N0} bytes → {OutputFileName}");
            MainViewModel.SetTaskbarProgress(100);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppendLog($"[실패] 추출 중 에러: {ex.Message}", LogLevel.Error);
            MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);

            return false;
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
            LogEntries.Add(new LogEntry { Message = msg, Level = level })
        );
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}