using RomForge.Core.UI.Command;
using Common.Localization;
using RomForge.Core.Models.Util;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RomForge.Core.Services;
using RomForge.Core.Models.N3DS;
using Common.Models;

namespace RomForge.ViewModels.Util;

public class CertsMainViewModel : BatchToolTabViewModel
{
    private const long CertOffset = 0x2040;
    private const int CertSize = 0xA00;
    private const string OutputFileName = "certs.bin";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cia" };

    private CancellationTokenSource _cts = new();

    public CertsFileItem? SelectedFile
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(HintVisibility));
            OnPropertyChanged(nameof(FileInfoVisibility));
            OnPropertyChanged(nameof(ProgressStatus));
            OnPropertyChanged(nameof(ProgressText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public TitleViewModel? RomInfo { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(RomInfoVisibility)); } }

    public bool IsExtracting { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); } }

    public Visibility HintVisibility => SelectedFile == null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FileInfoVisibility => SelectedFile != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RomInfoVisibility => RomInfo != null ? Visibility.Visible : Visibility.Collapsed;

    public static string OutputPath => Path.Combine(AppContext.BaseDirectory, OutputFileName);

    public string ProgressStatus => SelectedFile == null ? Loc.S.LabelCertsDropOrSelectFile : Loc.S.LogOutputFile(OutputPath);

    public string ProgressText => SelectedFile == null ? string.Empty : $"{SelectedFile.Progress}%";

    public static string FileDialogFilter => $"{Loc.S.FilterFilesOfType("CIA")}|*.cia|{Loc.S.FilterAllFiles}|*.*";

    public ICommand RunCommand { get; }

    public ICommand ClearFileCommand { get; }

    public CertsMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsExtracting && SelectedFile != null);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsExtracting);
        ClearFileCommand = new RelayCommand(_ => ClearFile(), _ => !IsExtracting);
    }

    public void SetFile(string path)
    {
        if (!SupportedExtensions.Contains(Path.GetExtension(path)))
        {
            AppendLog(Loc.S.ErrUnsupportedFileType(Path.GetExtension(path)), LogLevel.Error);
            _ = SoundPlayer.PlayErrorAsync();
            return;
        }

        if (!File.Exists(path))
        {
            AppendLog(Loc.S.ErrFileNotFoundName(path), LogLevel.Error);
            _ = SoundPlayer.PlayErrorAsync();
            return;
        }

        SelectedFile = new CertsFileItem(path);
        RomInfo = null;

        AppendLog(Loc.S.LogFileLoaded(SelectedFile.FileName));
        _ = LoadRomInfoAsync(path);
    }

    public void ClearFile()
    {
        SelectedFile = null;

        ClearLog();
    }

    private async Task LoadRomInfoAsync(string path)
    {
        try
        {
            var result = await Task.Run(() => Core.Services.N3DS.Util.ParseFile(path)) ?? throw new NullReferenceException();
            BitmapSource? icon = null;

            if (result.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result.IconPixels, 48 * 4);
                bitmap.Freeze();
                icon = bitmap;
            }

            RomInfo = new TitleViewModel
            {
                Title = result.Title,
                FilePath = result.FilePath,
                ShortDescription = result?.ShortDescription,
                Publisher = result?.Publisher,
                ProductCode = result?.ProductCode,
                Crypto = result!.Crypto,
                Icon = icon
            };
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogGameInfoLoadFailed(ex.Message), LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
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

        bool canceled = false;
        bool failed = false;

        using (BeginWork())
        {
            try
            {
                SelectedFile.State = ItemStatus.Extracting;
                SelectedFile.Progress = 0;

                BeginBatch(1);
                StartBatchItem();
                AppendLog(Loc.S.LogCertsExtractStart(SelectedFile.FileName), LogLevel.Highlight);
                AppendLog(Loc.S.LogCertsOffsetSize(CertOffset.ToString("X4"), CertSize.ToString("X3")));
                AppendLog(Loc.S.LogOutputPath(OutputPath));

                bool success = await Task.Run(() => ExtractCerts(SelectedFile, _cts.Token));

                if (success)
                {
                    SelectedFile.Progress = 100;
                    SelectedFile.State = ItemStatus.Done;

                    CompleteBatchItem();
                    await SoundPlayer.PlayCompleteAsync();
                    OnPropertyChanged(nameof(ProgressText));
                    AppendLog(Loc.S.LogCertsExtractDone(OutputFileName), LogLevel.Highlight);
                }
                else
                {
                    SelectedFile.Progress = 0;

                    CompleteBatchItem(false);
                    await SoundPlayer.PlayErrorAsync();

                    if (SelectedFile.State is ItemStatus.Extracting)
                        SelectedFile.State = ItemStatus.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                canceled = true;

                AppendLog(Loc.S.LogJobCancelled, LogLevel.Error);

                if (SelectedFile.State is ItemStatus.Extracting)
                {
                    SelectedFile.State = ItemStatus.Cancelled;
                    SelectedFile.Progress = 0;
                }
            }
            catch (Exception ex)
            {
                AppendLog(Loc.S.LogError(ex.Message), LogLevel.Error);
                await SoundPlayer.PlayErrorAsync();
                failed = true;

                SelectedFile.State = ItemStatus.Failed;
            }
            finally
            {
                IsExtracting = false;

                EndBatch(canceled, failed);
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
                AppendLog(Loc.S.LogCertsFileTooSmall((CertOffset + CertSize).ToString("X"), fs.Length.ToString("X")), LogLevel.Error);
                item.State = ItemStatus.Failed;

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
                AppendLog(Loc.S.LogCertsIncompleteRead(CertSize.ToString("X"), totalRead.ToString("X")), LogLevel.Error);
                item.State = ItemStatus.Failed;

                return false;
            }

            ct.ThrowIfCancellationRequested();

            File.WriteAllBytes(OutputPath, buffer);
            AppendLog(Loc.S.LogCertsSuccess(totalRead.ToString("N0"), OutputFileName));
            _ = SoundPlayer.PlayCompleteAsync();

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppendLog(Loc.S.LogCertsExtractError(ex.Message), LogLevel.Error);
            return false;
        }
    }
}