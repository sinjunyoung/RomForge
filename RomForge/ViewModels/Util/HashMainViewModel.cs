using Common;
using Common.WPF.ViewModels;
using RomForge.Helpers;
using RomForge.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels.Util;

public class HashMainViewModel : ToolTabViewModel
{
    #region Fields

    private bool _isConverting;
    private HashAlgorithmType _selectedAlgorithm = HashAlgorithmType.MD5;
    private CancellationTokenSource _cts = new();

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<HashFileItem> FileItems { get; } = [];

    #endregion

    #region Properties

    public bool IsConverting
    {
        get => _isConverting;
        set { _isConverting = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLocked)); CommandManager.InvalidateRequerySuggested(); }
    }

    public HashAlgorithmType SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set { _selectedAlgorithm = value; OnPropertyChanged(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }

    #endregion

    public event Action<HashFileItem>? ScrollToItemRequested;

    #region Constructor

    public HashMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsConverting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsConverting);
    }

    #endregion

    #region Public Methods

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandPaths(paths))
        {
            if (!existing.Add(path))
                continue;

            var vm = new HashFileItem(path)
            {
                No = FileItems.Count + 1
            };

            FileItems.Add(vm);
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<HashFileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        int no = 1;
        foreach (var item in FileItems)
            item.No = no++;

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    public static string GetFileDialogFilter()
    {
        return "모든 파일|*.*";
    }

    #endregion

    #region Private Methods

    private async Task RunAsync()
    {
        IsConverting = true;
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        var algoType = SelectedAlgorithm;
        AppendLog($"총 {FileItems.Count}개의 파일 해시 계산을 시작합니다. (알고리즘: {algoType})", LogLevel.Highlight);

        using (BeginWork())
        {
            try
            {
                int successCount = 0;
                var token = _cts.Token;

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = token
                };

                await Parallel.ForEachAsync(FileItems, parallelOptions, async (item, cancellationToken) =>
                {
                    item.Status = "대기중";
                    item.Progress = 0;
                    item.HashResult = string.Empty;
                    item.Status = "변환중";

                    Application.Current?.Dispatcher.BeginInvoke(() => ScrollToItemRequested?.Invoke(item));

                    string result = await Task.Run(() => ComputeHash(item, algoType, cancellationToken), cancellationToken);

                    if (!string.IsNullOrEmpty(result))
                    {
                        item.HashResult = result;
                        item.Progress = 100;
                        item.Status = "완료";
                        Interlocked.Increment(ref successCount);
                        AppendLog($"[완료] {item.FileName} -> {result}");
                    }
                    else
                    {
                        item.Progress = 0;
                        item.Status = "실패";
                        AppendLog($"[실패] {item.FileName} 해시 계산 오류", LogLevel.Error);
                    }
                });

                AppendLog($"작업 완료 (성공: {successCount} / 전체: {FileItems.Count})", LogLevel.Highlight);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "대기중" || i.Status == "변환중"))
                {
                    item.Status = "취소";
                    item.Progress = 0;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"오류 발생: {ex.Message}", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "변환중"))
                    item.Status = "실패";
            }
            finally
            {
                IsConverting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private static string ComputeHash(HashFileItem item, HashAlgorithmType algoType, CancellationToken token)
    {
        try
        {
            if (!File.Exists(item.FilePath)) 
                return string.Empty;

            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            long totalBytes = fs.Length;

            if (algoType == HashAlgorithmType.CRC32)
            {
                var crc = new System.IO.Hashing.Crc32();
                return ProcessNonCryptoStream(fs, totalBytes, item, (buf, len) => crc.Append(new ReadOnlySpan<byte>(buf, 0, len)), () =>
                {
                    byte[] hashBytes = crc.GetHashAndReset();
                    return BitConverter.ToUInt32([.. hashBytes.Reverse()], 0).ToString("X8");
                }, token);
            }

            if (algoType == HashAlgorithmType.BLAKE3)
            {
                using var hasher = Blake3.Hasher.New();
                return ProcessNonCryptoStream(fs, totalBytes, item, (buf, len) => hasher.Update(new ReadOnlySpan<byte>(buf, 0, len)), () =>
                {
                    return hasher.Finalize().ToString().ToUpperInvariant();
                }, token);
            }

            using var algorithm = CreateHashAlgorithm(algoType);

            if (algorithm == null) 
                return string.Empty;

            byte[] buffer = new byte[1024 * 64];
            long totalRead = 0;
            int read;

            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                totalRead += read;

                if (totalRead == totalBytes)
                    algorithm.TransformFinalBlock(buffer, 0, read);
                else
                    algorithm.TransformBlock(buffer, 0, read, buffer, 0);

                if (totalBytes > 0)
                {
                    int newProgress = (int)((totalRead * 100) / totalBytes);

                    if (item.Progress != newProgress)
                        item.Progress = newProgress;
                }
            }

            byte[]? cryptoBytes = algorithm.Hash;

            if (cryptoBytes == null) 
                return string.Empty;

            return ConvertToHexString(cryptoBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ProcessNonCryptoStream(FileStream fs, long totalBytes, HashFileItem item, Action<byte[], int> appendAction, Func<string> finalizeAction, CancellationToken token)
    {
        byte[] buffer = new byte[1024 * 64];
        long totalRead = 0;
        int read;

        while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            totalRead += read;

            appendAction(buffer, read);

            if (totalBytes > 0)
                item.Progress = (int)((totalRead * 100) / totalBytes);
        }

        return finalizeAction();
    }

    private static HashAlgorithm? CreateHashAlgorithm(HashAlgorithmType type)
    {
        return type switch
        {
            HashAlgorithmType.MD5 => MD5.Create(),
            HashAlgorithmType.SHA1 => SHA1.Create(),
            HashAlgorithmType.SHA256 => SHA256.Create(),
            _ => null
        };
    }

    private static string ConvertToHexString(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);

        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));

        return sb.ToString().ToUpperInvariant();
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", options))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
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