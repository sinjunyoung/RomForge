using _3DS.Core.Crypto;
using _3DS.Core.IO;
using _3DS.Core.Models;
using Common;
using System.IO.Pipelines;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace _3DS.Core.Services;

public static class Z3dsCompressor
{
    private static readonly byte[] MagicNcsd = "NCSD"u8.ToArray();

    private const string CompressExtension = ".zcci";

    private const int FrameSize = 32 * 1024 * 1024;

    public static async Task CompressAsync(string inputPath, int compressionLevel = 18, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        string? outputPath = null;
        bool isCompleted = false;

        try
        {
            var fileStream = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Stream inputStream = fileStream;
            byte[] headerBuffer = new byte[0x200];

            fileStream.Position = 0x4000;

            await fileStream.ReadExactlyAsync(headerBuffer, 0, 0x200, ct);

            var header = NcchHeader.Parse(headerBuffer, 0);

            if (!header.NoCrypto)
            {
                log?.Invoke("암호화된 롬 감지, 복호화 파이프라인 구동...", LogLevel.Info);

                var keyStore = KeyStoreProvider.Instance.KeyStore;

                fileStream.Position = 0;

                var ncsdHeader = new SubStream(fileStream, 0, 0x4000);
                var ncchDecrypted = new NcchDecryptionStream(fileStream, 0x4000, keyStore);

                inputStream = new ConcatStream(ncsdHeader, ncchDecrypted);
            }
            else
            {
                fileStream.Position = 0;
            }

            await using (inputStream)
            {
                outputPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, CompressExtension));

                using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write);

                log?.Invoke($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);
                await CompressInternalAsync(inputStream, outputStream, fileStream.Length, compressionLevel, progress, ct);

                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = new FileInfo(outputPath).Length;

                log?.Invoke($"압축률: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight);
                log?.Invoke($"압축 완료: {outputPath}", LogLevel.Ok);
            }

            isCompleted = true;
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    public static async Task CompressFromCiaAsync(string inputPath, int compressionLevel = 18, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        string? outputPath = null;
        bool isCompleted = false;

        try
        {
            var keyStore = KeyStoreProvider.Instance.KeyStore;
            var unpacker = new CiaReader(keyStore);
            await using var ctx = await unpacker.OpenAsync(inputPath, log, ct);
            uint titleType = (uint)(ctx.Ticket.TitleId >> 32);

            if (titleType != 0x00040000)
            {
                string typeDescription = titleType switch
                {
                    0x0004000E => "업데이트",
                    0x0004008C => "DLC",
                    _ => $"미지원 콘텐츠 타입 (Type ID: 0x{titleType:X8})"
                };

                throw new NotSupportedException($"{typeDescription} 파일은 CCI 복원이 불가능합니다. (본편만 가능)");
            }

            outputPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, CompressExtension));

            using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 64 * 1024 * 1024, resumeWriterThreshold: 32 * 1024 * 1024));
            var buildTask = Task.Run(async () =>
            {
                try
                {
                    await NcsdBuilder.BuildAsync(ctx, new PipeWriterStream(pipe.Writer), null, ct);
                    await pipe.Writer.CompleteAsync();
                }
                catch (Exception ex) { await pipe.Writer.CompleteAsync(ex); }
            }, ct);

            long uncompressedSize = NcsdBuilder.CalculateOutputSize(ctx);

            log?.Invoke($"{Path.GetFileName(inputPath)} 압축 시작", LogLevel.Highlight);
            await CompressInternalAsync(new PipeReaderStream(pipe.Reader), outputStream, uncompressedSize, compressionLevel, progress, ct);

            await buildTask;

            long originalSize = new FileInfo(inputPath).Length;
            long compressedSize = new FileInfo(outputPath).Length;

            log?.Invoke($"압축률: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight);
            log?.Invoke($"압축 완료: {outputPath}", LogLevel.Ok);

            isCompleted = true;
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    private static async Task CompressInternalAsync(Stream input, Stream output, long uncompressedSize, int compressionLevel, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        long readBytes = 0;
        long writtenBytes = 0;
        byte[] underlyingMagic = MagicNcsd;

        if (input.CanSeek && input.Length > 0x104)
        {
            input.Position = 0x100;

            byte[] magicBuf = new byte[4];

            await input.ReadExactlyAsync(magicBuf, 0, 4, ct);

            underlyingMagic = magicBuf;
            input.Position = 0;
        }

        byte[] metadata = Z3dsFormat.BuildMetadata(FrameSize);
        int metadataAligned = Z3dsFormat.AlignUp(metadata.Length, 16);
        byte[] metadataPadded = new byte[metadataAligned];

        metadata.CopyTo(metadataPadded, 0);

        long headerOffset = output.Position;

        await output.WriteAsync(new byte[0x20 + metadataAligned], ct);

        long bodyStartOffset = output.Position;
        int blockCount = (int)((uncompressedSize + FrameSize - 1) / FrameSize);
        var tasks = new Task<(byte[] data, int size, int index)>[blockCount];
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);

        for (int i = 0; i < blockCount; i++)
        {
            int size = (int)Math.Min(FrameSize, uncompressedSize - (long)i * FrameSize);
            byte[] buf = new byte[size];

            await input.ReadExactlyAsync(buf, 0, size, ct);

            Interlocked.Add(ref readBytes, size);
            progress?.Report(new ProgressInfo { Percent = (int)((double)Interlocked.Read(ref readBytes) / uncompressedSize * 50.0) });

            tasks[i] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var compressor = new Compressor(compressionLevel);

                    compressor.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 25);

                    int maxBound = Compressor.GetCompressBound(size);
                    byte[] compBuf = new byte[maxBound];
                    int compSize = compressor.Wrap(buf, compBuf);
                    byte[] result = new byte[compSize];

                    Array.Copy(compBuf, result, compSize);

                    return (result, size, i);
                }
                finally { semaphore.Release(); }
            }, ct);
        }

        var seekEntries = new SeekEntry[blockCount];

        for (int i = 0; i < blockCount; i++)
        {
            var (data, size, index) = await tasks[i];

            await output.WriteAsync(data, ct);

            seekEntries[i] = new SeekEntry { CompressedSize = (uint)data.Length, DecompressedSize = (uint)size };

            Interlocked.Add(ref writtenBytes, size);
            progress?.Report(new ProgressInfo { Percent = 50 + (int)((double)Interlocked.Read(ref writtenBytes) / uncompressedSize * 50.0) });
        }

        Z3dsFormat.WriteSeekTable(output, [.. seekEntries]);

        long endOffset = output.Position;
        output.Position = headerOffset;

        Z3dsFormat.WriteZ3dsHeader(output, underlyingMagic, (uint)metadataAligned, endOffset - bodyStartOffset, uncompressedSize);
        await output.WriteAsync(metadataPadded, ct);

        output.Position = endOffset;

        progress?.Report(new ProgressInfo { Percent = 100 });
    }
}