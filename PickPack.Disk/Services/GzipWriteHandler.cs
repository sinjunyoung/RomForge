using ICSharpCode.SharpZipLib.GZip;
using PickPack.Disk.Helpers;
using PickPack.Disk.Interfaces;
using PickPack.Disk.Models;

namespace PickPack.Disk.Services;

public class GzipWriteHandler(Action<int, string?, string?> progressCallback) : IImageWriteHandler
{
    public async Task<(Stream stream, long length)> OpenStreamAsync(string imagePath, CancellationToken ct)
    {
        progressCallback(0, "파일 크기 계산 중...", string.Empty);

        long sourceLength = await GetGzUncompressedSizeAsync(imagePath, ct);
        var compressedStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, Optimal.BufferSize * 2, FileOptions.SequentialScan);
        var gzStream = new GZipInputStream(compressedStream);

        return (gzStream, sourceLength);
    }

    private async Task<long> GetGzUncompressedSizeAsync(string imagePath, CancellationToken ct)
    {
        var sizeProgressReporter = new ProgressReporter();

        sizeProgressReporter.Initialize(ct);
        sizeProgressReporter.ProgressChanged += (sender, args) => progressCallback(args.Percent, args.Message1, args.Message2);

        long totalReadBytes = 0;
        byte[] buffer = Optimal.ArrayPool.Rent(Optimal.BufferSize * 2);
        long compressedFileSize = new FileInfo(imagePath).Length;

        progressCallback(0, "이미지 크기 확인 중...", string.Empty);

        try
        {
            using var compressedStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, Optimal.BufferSize * 2);
            using var gzStream = new GZipInputStream(compressedStream);
            int readBytes;

            while ((readBytes = await gzStream.ReadAsync(buffer, ct)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                totalReadBytes += readBytes;

                sizeProgressReporter.ReportProgressWithInterval(compressedStream.Position, compressedFileSize, "이미지 크기 확인 중...", 1.0);
            }
        }
        finally
        {
            Optimal.ArrayPool.Return(buffer);
        }

        return totalReadBytes;
    }
}