using _3DS.Core.Crypto;
using _3DS.Core.IO;
using _3DS.Core.Services;
using Common;
using Common.WPF.ViewModels;
using RomForge.Helpers;
using RomForge.Models;
using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class DecryptorMainViewModel : ToolTabViewModel
{
    #region Fields

    private bool _isDecrypting;
    private CancellationTokenSource _cts = new();

    #endregion

    #region Collections

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    public ObservableCollection<DecryptorFileItem> FileItems { get; } = [];

    #endregion

    #region Properties

    public bool IsDecrypting
    {
        get => _isDecrypting;
        set { _isDecrypting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region Commands

    public ICommand RunCommand { get; }

    #endregion

    public event Action<DecryptorFileItem>? ScrollToItemRequested;

    #region Constructor

    public DecryptorMainViewModel()
    {
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsDecrypting && FileItems.Count > 0);
        CancelCommand = new RelayCommand(_ => _cts.Cancel(), _ => IsDecrypting);
    }

    #endregion

    #region Public Methods

    public async Task AddPaths(IEnumerable<string> paths)
    {
        var existing = FileItems.Select(f => f.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandPaths(paths))
        {
            string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

            if (ext is not ("3ds" or "cci" or "cia"))
                continue;

            if (!existing.Add(path))
                continue;

            try
            {
                var result = await Util.ParseFile(path);
                var vm = new DecryptorFileItem(path)
                {
                    TitleId = result.Title!.TitleId,
                    ProductCode = result.ProductCode,
                    ShortDescription = result.ShortDescription,
                    Publisher = result.Publisher,
                    Crypto = result.Crypto
                };

                if (result?.IconPixels is not null)
                {
                    var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result.IconPixels, 48 * 4);
                    bitmap.Freeze();
                    vm.Icon = bitmap;
                }

                FileItems.Add(vm);

                for (int i = 0; i < FileItems.Count; i++)
                    FileItems[i].No = i + 1;
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
            }
        }

        OnPropertyChanged(nameof(HintVisibility));
        CommandManager.InvalidateRequerySuggested();
    }

    public void RemoveItems(IEnumerable<DecryptorFileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        for (int i = 0; i < FileItems.Count; i++)
            FileItems[i].No = i + 1;

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void ClearItems()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    #endregion

    #region Private Methods

    private async Task RunAsync()
    {
        IsDecrypting = true;
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        ClearLog();

        using (BeginWork())
        {
            try
            {
                int totalCount = FileItems.Count;

                AppendLog($"총 {totalCount}개의 복호화 작업을 시작합니다.", LogLevel.Highlight);

                int cnt = 0;

                foreach (var item in FileItems)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    if (item.Status is "완료" or "미지원")
                        continue;

                    if (!item.Crypto)
                    {
                        AppendLog($"[{item.FileName}] 이미 복호화된 파일입니다.", LogLevel.Info);
                        item.Status = "미지원";
                        continue;
                    }

                    item.Progress = 0;
                    item.Status = "복호화중";

                    ScrollToItemRequested?.Invoke(item);

                    try
                    {
                        string outputPath = Path.Combine(item.Directory, $"{item.FileName}_dec.{item.Extension}");
                        outputPath = Utils.GetUniqueFilePath(outputPath);

                        await DecryptAsync(item, outputPath, _cts.Token);

                        item.Progress = 100;
                        item.Status = "완료";
                        cnt++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[{item.FileName}] 복호화 실패: {ex.Message}", LogLevel.Error);
                        item.Status = "실패";
                        item.Progress = 0;
                    }
                }

                if (cnt > 0)
                    AppendLog($"총 {cnt}개의 작업을 성공적으로 완료했습니다.", LogLevel.Ok);
                else
                    AppendLog("성공한 작업이 없습니다.", LogLevel.Error);
            }
            catch (OperationCanceledException)
            {
                AppendLog("작업이 취소되었습니다.", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status is "대기중" or "복호화중"))
                    item.Status = "취소";
            }
            catch (Exception ex)
            {
                AppendLog($"오류: {ex.Message}", LogLevel.Error);
                foreach (var item in FileItems.Where(i => i.Status == "복호화중"))
                    item.Status = "실패";
            }
            finally
            {
                IsDecrypting = false;
            }
        }
    }

    private static async Task DecryptAsync(DecryptorFileItem item, string outputPath, CancellationToken ct)
    {
        KeyStore keyStore = new();
        const int bufferSize = 1024 * 1024;
        long written = 0;

        string ext = item.Extension;

        if (ext is "3ds" or "cci")
        {
            long totalBytes = item.FileSizeBytes;
            await using var inputStream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            byte[] ncsdBuf = new byte[0x200];

            await inputStream.ReadExactlyAsync(ncsdBuf, ct);

            await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);

            await outputStream.WriteAsync(ncsdBuf, ct);
            written += ncsdBuf.Length;

            const int MediaUnit = 0x200;
            var partitionMap = new (uint offset, uint size)[8];

            for (int i = 0; i < 8; i++)
            {
                int off = 0x120 + i * 8;
                partitionMap[i] = (BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(off)), BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(off + 4)));
            }

            long firstPartOffset = partitionMap.Where(p => p.offset > 0).Min(p => (long)p.offset) * MediaUnit;

            if (firstPartOffset > ncsdBuf.Length)
            {
                byte[] gap = new byte[firstPartOffset - ncsdBuf.Length];

                inputStream.Position = ncsdBuf.Length;

                await inputStream.ReadExactlyAsync(gap, ct);
                await outputStream.WriteAsync(gap, ct);

                written += gap.Length;
            }

            for (int i = 0; i < 8; i++)
            {
                var (partOffset, partSize) = partitionMap[i];

                if (partOffset == 0 || partSize == 0)
                    continue;

                long byteOffset = (long)partOffset * MediaUnit;
                long byteSize = (long)partSize * MediaUnit;

                if (outputStream.Position < byteOffset)
                {
                    long gapSize = byteOffset - outputStream.Position;
                    byte[] gap = new byte[Math.Min(gapSize, bufferSize)];
                    long remaining = gapSize;

                    inputStream.Position = outputStream.Position;

                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(gap.Length, remaining);

                        await inputStream.ReadExactlyAsync(gap.AsMemory(0, toRead), ct);
                        await outputStream.WriteAsync(gap.AsMemory(0, toRead), ct);

                        remaining -= toRead;
                        written += toRead;
                        item.Progress = totalBytes > 0 ? (int)(written * 100 / totalBytes) : 0;
                    }
                }

                await using var partStream = new SubStream(inputStream, byteOffset, byteSize);
                await using Stream decStream = new NcchDecryptionStream(partStream, 0, keyStore);
                byte[] buf = new byte[bufferSize];
                int bytesRead;

                while ((bytesRead = await decStream.ReadAsync(buf, ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    await outputStream.WriteAsync(buf.AsMemory(0, bytesRead), ct);

                    written += bytesRead;
                    item.Progress = totalBytes > 0 ? (int)(written * 100 / totalBytes) : 0;
                }
            }
        }
        else if (ext == "cia")
        {
            var unpacker = new CiaReader(keyStore);
            await using var ctx = await unpacker.OpenAsync(item.FilePath, null, ct);
            long totalBytes = ctx.Contents.Sum(c => (long)c.ContentSize);
            await using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
            byte[] buf = new byte[bufferSize];

            foreach (var content in ctx.Contents)
            {
                ct.ThrowIfCancellationRequested();

                var (contentStream, contentSize) = await ctx.OpenContent(content.ContentIndex, null);
                await using (contentStream)
                {
                    long remaining = contentSize;

                    while (remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buf.Length, remaining);
                        int bytesRead = await contentStream.ReadAsync(buf.AsMemory(0, toRead), ct);

                        if (bytesRead == 0)
                            break;

                        await outputStream.WriteAsync(buf.AsMemory(0, bytesRead), ct);
                        written += bytesRead;
                        remaining -= bytesRead;
                        item.Progress = totalBytes > 0 ? (int)(written * 100 / totalBytes) : 0;
                    }
                }
            }
        }
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

        Application.Current.Dispatcher.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));
    }

    private void ClearLog()
    {
        if (Application.Current?.Dispatcher == null)
            return;

        Application.Current.Dispatcher.Invoke(() => LogEntries.Clear());
    }

    #endregion
}