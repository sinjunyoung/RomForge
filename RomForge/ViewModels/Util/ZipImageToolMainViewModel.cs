using Common;
using Common.WPF.ViewModels;
using Ionic.Zlib;
using NSW.WPF.Services;
using NSW.WPF.UI;
using PickPack.Disk;
using PickPack.Disk.ETC;
using RomForge.Core.UI.Command;
using RomForge.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace RomForge.ViewModels.Util;

public class ZipImageToolMainViewModel : ToolTabViewModel
{
    #region Fields

    private readonly WmiWatchers _wmiWatchers = new();
    private CancellationTokenSource? _cts;

    #endregion

    #region USB

    public ObservableCollection<DriveInfos> UsbDrives { get; } = [];

    private DriveInfos? _selectedDrive;
    public DriveInfos? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (_selectedDrive != value)
            {
                _selectedDrive = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Status

    private string _statusText1 = string.Empty;
    public string StatusText1
    {
        get => _statusText1;
        set { _statusText1 = value; OnPropertyChanged(); }
    }

    private string _statusText2 = string.Empty;
    public string StatusText2
    {
        get => _statusText2;
        set { _statusText2 = value; OnPropertyChanged(); }
    }

    private int _progress;
    public int Progress
    {
        get => _progress;
        set 
        { 
            _progress = value; 
            MainViewModel.SetTaskbarProgress(value);
            OnPropertyChanged();
        }
    }

    #endregion

    #region Tab

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    #endregion

    #region Write Tab

    private string _writeImagePath = string.Empty;
    public string WriteImagePath
    {
        get => _writeImagePath;
        set { _writeImagePath = value; OnPropertyChanged(); }
    }

    private BitmapImage _writeFileIcon = new(new Uri("pack://application:,,,/Assets/Images/File.png", UriKind.Absolute));
    public BitmapImage? WriteFileIcon
    {
        get => _writeFileIcon;
        set { _writeFileIcon = value; OnPropertyChanged(); }
    }

    private string _writeButtonText = "굽기";
    public string WriteButtonText
    {
        get => _writeButtonText;
        set { _writeButtonText = value; OnPropertyChanged(); }
    }

    #endregion

    #region Read Tab

    private string _readImagePath = string.Empty;
    public string ReadImagePath
    {
        get => _readImagePath;
        set
        {
            _readImagePath = value;
            OnPropertyChanged();
            UpdateSegmentOptionsVisibility(value);
        }
    }

    private BitmapImage _readFileIcon = new(new Uri("pack://application:,,,/Assets/Images/File.png", UriKind.Absolute));
    public BitmapImage? ReadFileIcon
    {
        get => _readFileIcon;
        set { _readFileIcon = value; OnPropertyChanged(); }
    }

    private bool _segmentOptionsVisible;
    public bool SegmentOptionsVisible
    {
        get => _segmentOptionsVisible;
        set { _segmentOptionsVisible = value; OnPropertyChanged(); }
    }

    private string _readButtonText = "저장";
    public string ReadButtonText
    {
        get => _readButtonText;
        set { _readButtonText = value; OnPropertyChanged(); }
    }

    public List<string> SegmentSizeOptions { get; } = ["분할안함", "1GB", "2GB", "5GB", "10GB", "20GB", "50GB", "100GB"];

    private int _selectedSegmentIndex = 5;
    public int SelectedSegmentIndex
    {
        get => _selectedSegmentIndex;
        set { _selectedSegmentIndex = value; OnPropertyChanged(); }
    }

    public List<string> CompressionLevelOptions { get; } = ["압축 안함", "빠르게", "보통 압축률", "최대 압축률"];

    private int _selectedCompressionIndex = 0;
    public int SelectedCompressionIndex
    {
        get => _selectedCompressionIndex;
        set { _selectedCompressionIndex = value; OnPropertyChanged(); }
    }

    #endregion

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    #region Commands

    public ICommand OpenWriteFileCommand { get; }
    public ICommand WriteCommand { get; }
    public ICommand OpenReadFileCommand { get; }
    public ICommand ReadCommand { get; }

    #endregion

    public ZipImageToolMainViewModel()
    {
        DiskUtil.PreventSleep();

        _wmiWatchers.USBArrival += (s, e) =>
        {
            AppendLog("이동식 드라이브의 연결이 감지되었습니다.\n드라이브 목록을 갱신합니다.");
        };

        _wmiWatchers.USBRemoval += (s, e) =>
        {
            AppendLog("이동식 드라이브의 분리가 감지되었습니다.\n드라이브 목록을 갱신합니다.");
        };

        OpenWriteFileCommand = new RelayCommand(_ => OpenWriteFile());
        OpenReadFileCommand = new RelayCommand(_ => OpenReadFile());
        WriteCommand = new ToggleCommand(async _ => await WriteAsync());
        ReadCommand = new ToggleCommand(async _ => await ReadAsync());

        ListRemovableUsbDrives();
    }

    #region Private Methods

    private void ListRemovableUsbDrives()
    {        
        UsbDrives.Clear();

        var infos = DriveInfos.GetDriveInfos();

        foreach (var info in infos)
            UsbDrives.Add(info);

        if (UsbDrives.Count > 0)
            SelectedDrive = UsbDrives[^1];
    }

    private long GetMaxOutputSegmentSize64() => SelectedSegmentIndex switch
    {
        0 => 0,
        1 => 1073741824,
        2 => 2 * FileSize._1GB,
        3 => 5 * FileSize._1GB,
        4 => 10 * FileSize._1GB,
        5 => 20 * FileSize._1GB,
        6 => 50 * FileSize._1GB,
        7 => 100 * FileSize._1GB,
        _ => 20 * FileSize._1GB
    };

    private CompressionLevel GetCompressionLevel() => SelectedCompressionIndex switch
    {
        0 => CompressionLevel.None,
        1 => CompressionLevel.BestSpeed,
        2 => CompressionLevel.Default,
        3 => CompressionLevel.BestCompression,
        _ => CompressionLevel.None
    };

    private void OpenWriteFile()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "ZIP 파일 (*.zip)|*.zip|gz 파일 (*.gz)|*.gz|디스크 이미지 파일 (*.img)|*.img|모든 파일 (*.*)|*.*"
        };

        if (ofd.ShowDialog() != true) 
            return;

        WriteImagePath = ofd.FileName;
        WriteFileIcon = GetIconForExtension(Path.GetExtension(ofd.FileName));
    }

    private static BitmapImage GetIconForExtension(string ext)
    {
        string name = ext.TrimStart('.').ToUpperInvariant();
        var uri = new Uri($"pack://application:,,,/Assets/Images/{name}.png", UriKind.Absolute);

        try 
        { 
            return new BitmapImage(uri); 
        }
        catch 
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/Images/File.png", UriKind.Absolute)); 
        }
    }

    private async Task WriteAsync()
    {
        if (IsLocked)
        {
            _cts?.Cancel();
            StatusText1 = "취소중...";
            return;
        }

        if (SelectedDrive == null)
        {
            AppendLog("SD 카드를 선택해주세요.");
            return;
        }

        if (!MessageBoxHelper.ShowQuestion("굽기를 시작하면 선택한 드라이브의 모든 데이터가 삭제 됩니다.\n진행 할까요?"))
            return;

        WriteButtonText = "취소";
        _cts = new CancellationTokenSource();

        using (BeginWork())
        {
            WriteButtonText = "취소";

            try
            {
                ImageWriter imgWriter = new();
                imgWriter.ProgressChanged += OnProgressChanged;
                imgWriter.WriteEnded += OnWriteEnded;

                AppendLog("굽기가 시작 됩니다.", LogLevel.Highlight);
                await imgWriter.WriteImageAsync(WriteImagePath, SelectedDrive.DiskNumber, SelectedDrive.SizeBytes, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Progress = 0;
                StatusText1 = "굽기 취소";
                PartitionUtil.RescanDisk(SelectedDrive.DiskNumber);
                AppendLog("굽기가 취소 되었습니다.", LogLevel.Error);
            }
            catch (Win32Exception ex)
            {
                AppendLog(ex.Message, LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);

            }
            catch (Exception ex)
            {
                AppendLog(ex.Message, LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
            }
            finally
            {
                WriteButtonText = "굽기";
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private void OpenReadFile()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "디스크 이미지 파일 (*.img)|*.img|ZIP 파일 (*.zip)|*.zip"
        };

        if (sfd.ShowDialog() != true) 
            return;

        string ext = Path.GetExtension(sfd.FileName).TrimStart('.').ToLowerInvariant();

        ReadImagePath = sfd.FileName;
        ReadFileIcon = GetIconForExtension(Path.GetExtension(sfd.FileName));

        if (ext != "img" && ext != "zip")
            AppendLog("지원하지 않는 파일 형식입니다.(.zip 또는 .img)", LogLevel.Error);
    }

    private async Task ReadAsync()
    {
        if (IsLocked)
        {
            _cts?.Cancel();
            StatusText1 = "취소중...";
            return;
        }

        if (SelectedDrive == null)
        {
            AppendLog("SD 카드를 선택해주세요.", LogLevel.Info);
            return;
        }

        if (string.IsNullOrEmpty(ReadImagePath))
        {
            AppendLog("이미지 파일을 지정해주세요.", LogLevel.Info);
            return;
        }

        ulong freeSpace = 0;

        try
        {
            freeSpace = DiskUtil.GetAvailableFreeSpace(Path.GetPathRoot(ReadImagePath));
        }
        catch (Exception ex)
        {
            AppendLog($"디스크 용량을 확인하는 중 오류가 발생했습니다: {ex.Message}", LogLevel.Error);
        }

        if (freeSpace < (ulong)SelectedDrive.SizeBytes)
        {
            AppendLog("디스크 용량이 부족합니다.", LogLevel.Error);
            return;
        }

        ReadButtonText = "취소";
        _cts = new CancellationTokenSource();

        using (BeginWork())
        {
            try
            {
                ImageReader reader = new();

                reader.ProgressChanged += OnProgressChanged;
                reader.WriteEnded += OnReaderEnded;

                long maxSegment = GetMaxOutputSegmentSize64();

                CompressionLevel compLevel = GetCompressionLevel();

                AppendLog("저장이 시작 됩니다.", LogLevel.Highlight);
                await Task.Run(() => reader.ReadImageAsync(SelectedDrive.DiskNumber, ReadImagePath, maxSegment, compLevel, _cts.Token));
            }
            catch (OperationCanceledException)
            {
                Progress = 0;
                StatusText1 = "저장 취소";
                DeleteArchiveTempAndPartialFiles(ReadImagePath);
                AppendLog("저장이 취소되었습니다.", LogLevel.Error);
            }
            catch (Win32Exception ex)
            {
                AppendLog(ex.Message, LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message, LogLevel.Error);
                MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
            }
            finally
            {
                ReadButtonText = "저장";
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private void OnProgressChanged(object? sender, ProgressEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Progress = e.Percent;
            StatusText1 = e.Message1;

            if (e.Message2 != null)
                StatusText2 = e.Message2;
        });
    }

    private void OnWriteEnded(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Progress = 100;
            StatusText1 = "이미지 굽기 완료";

            PartitionUtil.RescanDisk(SelectedDrive!.DiskNumber);
            PartitionUtil.AssignNextAvailableDriveLetter();

            AppendLog("이미지 굽기가 완료되었습니다.", LogLevel.Ok);
        });
    }

    private void OnReaderEnded(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Progress = 100;
            StatusText1 = "이미지 저장 완료";

            Path.GetDirectoryName(ReadImagePath).OpenFolder();

            AppendLog("이미지 저장이 완료되었습니다.", LogLevel.Ok);
        });
    }

    private void UpdateSegmentOptionsVisibility(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            SegmentOptionsVisible = false;
            return;
        }

        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        SegmentOptionsVisible = ext == "zip";
    }

    private static void DeleteArchiveTempAndPartialFiles(string fileName)
    {
        File.Delete(fileName);

        string? directoryPath = Path.GetDirectoryName(fileName);

        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            return;

        try
        {
            foreach (string file in Directory.GetFiles(directoryPath, "DotNetZip-*.tmp"))
            {
                try 
                { 
                    File.Delete(file); 
                }
                catch (IOException ex) 
                { 
                    Debug.WriteLine($"Could not delete temp file {file}: {ex.Message}");
                }
            }

            string searchPattern = Path.GetFileNameWithoutExtension(fileName) + ".z*";

            foreach (string file in Directory.GetFiles(directoryPath, searchPattern))
            {
                try 
                { 
                    File.Delete(file); 
                }
                catch (IOException ex) 
                { 
                    Debug.WriteLine($"Could not delete partial file {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"An unexpected error occurred: {ex.Message}");
        }
    }

    private void AppendLog(string msg, LogLevel level = LogLevel.Info)
    {
        if (Application.Current?.Dispatcher == null) 
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    #endregion
}