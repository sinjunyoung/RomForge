using Common;
using System.IO;
using System.IO.Compression;

namespace RomForge.Core.Services.Patch;

public class ZipCompressor(Action<string, LogLevel>? log, IProgress<ProgressInfo>? progress)
{
    public async Task CompressFromFileAsync(string? sourcePath, string outputPath, string outputDir, CancellationToken ct)
    {
        log?.Invoke($"압축 시작: {Path.GetFileName(sourcePath)}", LogLevel.Highlight);

        string zipPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(sourcePath) + ".zip");

        zipPath = Utils.GetUniqueFilePath(zipPath);

        await Task.Run(() =>
        {
            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(Path.GetFileName(sourcePath) ?? throw new NullReferenceException());
            using var entryStream = entry.Open();
            using var sourceStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[81920];
            long totalBytes = sourceStream.Length;
            long bytesReadTotal = 0;
            int bytesRead;
            var reporter = new ProgressReporter("압축 중...", string.Empty, totalBytes, progress);
            var report = reporter.CreateAction();

            while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                entryStream.Write(buffer, 0, bytesRead);

                bytesReadTotal += bytesRead;

                if (totalBytes > 0)
                    report(bytesReadTotal, totalBytes);
            }
        }, ct);

        log?.Invoke($"압축 완료: {zipPath}", LogLevel.Ok);
    }
}