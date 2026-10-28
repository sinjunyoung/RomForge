using Ionic.Zip;
using Ionic.Zlib;
using PickPack.Disk.Interfaces;
using PickPack.Disk.IO;
using PickPack.Disk.Models;
using System.Threading.Channels;

namespace PickPack.Disk.Services
{
    public class ZipReadHandler(long maxOutputSegmentSize, CompressionLevel compressionLevel, Action<long, long, string> progressReporter, CancellationToken ct) : IImageReaderHandler
    {
        public async Task WriteImageAsync(FileStream sourceStream, string outputPath, long totalSize, CancellationToken ct)
        {
            using var zipFile = new ZipFile();

            zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
            zipFile.CompressionLevel = compressionLevel;
            zipFile.MaxOutputSegmentSize64 = maxOutputSegmentSize;
            zipFile.BufferSize = Optimal.BufferSize;
            zipFile.CodecBufferSize = Optimal.BufferSize;            
            zipFile.ParallelDeflateThreshold = 50 * 1024 * 1024;
            zipFile.AlternateEncoding = null;
            zipFile.AlternateEncodingUsage = ZipOption.Never;
            zipFile.Strategy = CompressionStrategy.Default;

            string imgFileName = $"{Path.GetFileNameWithoutExtension(outputPath)}.img";
            var optimizedStream = new DirectReadStream(sourceStream, totalSize);

            zipFile.SaveProgress += (sender, e) => OnZipSaveProgress(e);

            zipFile.AddEntry(imgFileName, optimizedStream);            
            await Task.Run(() => zipFile.Save(outputPath), ct);
        }

        private void OnZipSaveProgress(SaveProgressEventArgs e)
        {            
            if (ct.IsCancellationRequested)
                e.Cancel = true;
            else
            {
                if (e.EventType == ZipProgressEventType.Saving_EntryBytesRead && e.TotalBytesToTransfer > 0)
                    progressReporter(e.BytesTransferred, e.TotalBytesToTransfer, "이미지 저장 중...");
            }
        }
    }

    public class RawImageReadHandler(Action<long, long, string> progressReporter) : IImageReaderHandler
    {
        const int ERROR_CRC = unchecked((int)0x80070017);

        const int ERROR_SECTOR_NOT_FOUND = unchecked((int)0x8007001B);

        public async Task WriteImageAsync(FileStream sourceStream, string outputPath, long totalSize, CancellationToken ct)
        {
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(Optimal.ChannelCapacity) 
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            var producerTask = ProduceDataAsync(sourceStream, channel.Writer, totalSize, ct);
            var consumerTask = ConsumeDataAsync(channel.Reader, outputPath, totalSize, ct);

            await consumerTask;
            await producerTask;
        }

        private static async Task ProduceDataAsync(Stream sourceStream, ChannelWriter<byte[]> writer, long totalSize, CancellationToken ct)
        {
            byte[] rentedBuffer = Optimal.ArrayPool.Rent(Optimal.BufferSize);
            long totalRead = 0;

            try
            {
                while (totalRead < totalSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int read;

                    try
                    {
                        read = await sourceStream.ReadAsync(rentedBuffer.AsMemory(0, Optimal.BufferSize), ct);

                        if (read == 0) 
                            break;
                    }
                    catch (IOException ex) when (ex.HResult == ERROR_CRC || ex.HResult == ERROR_SECTOR_NOT_FOUND)
                    {
                        Array.Clear(rentedBuffer, 0, Optimal.BufferSize);

                        read = Optimal.BufferSize;
                    }

                    if (read == Optimal.BufferSize)
                    {
                        await writer.WriteAsync(rentedBuffer, ct);

                        rentedBuffer = Optimal.ArrayPool.Rent(Optimal.BufferSize);
                    }
                    else
                    {
                        byte[] dataToSend = Optimal.ArrayPool.Rent(read);

                        Array.Copy(rentedBuffer, dataToSend, read);

                        await writer.WriteAsync(dataToSend, ct);
                    }

                    totalRead += read;
                }

                if (totalSize > totalRead)
                    await writer.WriteAsync([], ct);
            }
            finally
            {
                Optimal.ArrayPool.Return(rentedBuffer);
                writer.Complete();
            }
        }

        private async Task ConsumeDataAsync(ChannelReader<byte[]> reader, string outputPath, long totalSize, CancellationToken ct)
        {
            long totalWritten = 0;
            long zeroBytesWritten = 0;
            using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, Optimal.BufferSize, FileOptions.Asynchronous);

            await foreach (var buffer in reader.ReadAllAsync(ct))
            {
                try
                {
                    if (buffer.Length == 0)
                    {
                        long bytesRemaining = totalSize - totalWritten;

                        while (bytesRemaining > 0)
                        {
                            int writeCount = (int)Math.Min(Optimal.BufferSize, bytesRemaining);

                            await outStream.WriteAsync(Optimal.ZeroBuffer.AsMemory(0, writeCount), ct);

                            bytesRemaining -= writeCount;
                            zeroBytesWritten += writeCount;
                            progressReporter(totalWritten + zeroBytesWritten, totalSize, "이미지 저장 중...");
                        }
                    }
                    else
                    {
                        await outStream.WriteAsync(buffer, ct);

                        totalWritten += buffer.Length;

                        progressReporter(totalWritten, totalSize, "이미지 저장 중...");
                    }
                }
                finally
                {
                    if (buffer != Array.Empty<byte>())
                        Optimal.ArrayPool.Return(buffer);
                }
            }
        }
    }
}