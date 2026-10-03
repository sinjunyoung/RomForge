using PickPack.Disk.ETC;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace PickPack.Disk
{
    public struct DataChunk
    {
        public byte[] Buffer;
        public int ValidLength;
    }

    public class ImageWriter
    {
        #region Field
        readonly ProgressReporter _progressReporter;
        CancellationToken _ct;
        #endregion

        #region Property
        public string WorkTitle { get; set; } = "이미지 굽기";
        #endregion

        #region Constructor
        public ImageWriter()
        {
            _progressReporter = new ProgressReporter();
            _progressReporter.ProgressChanged += (sender, args) => OnProgressChanged(args.Percent, args.Message1, args.Message2);
        }
        #endregion

        #region Event
        public event EventHandler<ProgressEventArgs>? ProgressChanged;
        public event EventHandler<EventArgs>? WriteEnded;

        protected virtual void OnProgressChanged(int percent, string message1, string? message2 = "") => ProgressChanged?.Invoke(this, new ProgressEventArgs { Percent = percent, Message1 = message1, Message2 = message2 });

        protected virtual void OnWriteEnded() => WriteEnded?.Invoke(this, EventArgs.Empty);
        #endregion

        public async Task WriteImageAsync(string imagePath, int physicalDriveNumber, long diskSize, CancellationToken ct)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            _ct = ct;
            _progressReporter.Initialize(ct);

            await WriteImageAsyncInternal(imagePath, physicalDriveNumber, diskSize, ct);
            OnWriteEnded();
        }

        private async Task WriteImageAsyncInternal(string imagePathOrUrl, int physicalDriveNumber, long diskSize, CancellationToken ct)
        {
            if (!ImageWriterFactory.IsSupported(imagePathOrUrl))
            {
                string supportedTypes = string.Join(", ", ImageWriterFactory.GetSupportedExtensions());
                throw new NotSupportedException($"지원되지 않는 파일 형식입니다. 지원 형식: {supportedTypes}");
            }

            ct.ThrowIfCancellationRequested();

            var handler = ImageWriterFactory.GetHandler(imagePathOrUrl, OnProgressChanged) ?? throw new InvalidOperationException("적절한 핸들러를 찾을 수 없습니다.");
            var (sourceStream, sourceLength) = await handler.OpenStreamAsync(imagePathOrUrl, ct);

            if (sourceLength > diskSize)
            {
                sourceStream?.Dispose();
                throw new InvalidOperationException("이미지 파일 크기가 대상 드라이브보다 큽니다.");
            }

            OnProgressChanged(0, "파티션 삭제 중...");
            await PartitionUtil.DeleteAllPartitionsAsync(physicalDriveNumber);
            OnProgressChanged(0, "파티션 삭제 완료.");

            await WriteToPhysicalDiskAsync(sourceStream, sourceLength, physicalDriveNumber, ct);
        }

        private async Task WriteToPhysicalDiskAsync(Stream sourceStream, long sourceLength, int physicalDriveNumber, CancellationToken ct)
        {
            using (sourceStream)
            {
                string physicalDrivePath = $@"\\.\PhysicalDrive{physicalDriveNumber}";
                using var stream = new FileStream(physicalDrivePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1024 * 1024, FileOptions.WriteThrough);

                stream.Seek(0, SeekOrigin.Begin);

                int sectorSize = DiskUtil.GetSectorSize(stream.SafeFileHandle);
                long bytesToWrite = Math.Min(sourceLength, stream.Length);

                OnProgressChanged(5, "이미지 데이터 쓰는 중...");

                await WriteDataWithProgress(sourceStream, stream, bytesToWrite, 0, bytesToWrite, sectorSize, ct);

                _progressReporter.ReportCompletion($"{WorkTitle} 완료");
            }
        }

        private async Task WriteDataWithProgress(Stream sourceStream, FileStream targetStream, long remainingBytes, long headerSize, long totalBytes, int sectorSize, CancellationToken ct)
        {
            int sectorsPerBuffer = Math.Max(131072, sectorSize * 128);
            int bufferSize = sectorSize * sectorsPerBuffer;

            var channel = Channel.CreateBounded<DataChunk>(new BoundedChannelOptions(Optimal.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var producerTask = ProduceDataAsync(sourceStream, channel.Writer, bufferSize, sectorSize, remainingBytes, ct);
            var consumerTask = ConsumeDataAsync(channel.Reader, targetStream, remainingBytes, headerSize, totalBytes, ct);

            await producerTask;
            await consumerTask;
        }

        private static async Task ProduceDataAsync(Stream fs, ChannelWriter<DataChunk> writer, int bufferSize, int sectorSize, long remainingBytes, CancellationToken ct)
        {
            byte[] buffer = Optimal.ArrayPool.Rent(bufferSize);
            long totalRead = 0;

            try
            {
                int read;

                while (totalRead < remainingBytes && (read = await fs.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bufferSize, remainingBytes - totalRead)), ct)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    totalRead += read;

                    if (read == bufferSize)
                    {
                        await writer.WriteAsync(new DataChunk { Buffer = buffer, ValidLength = bufferSize }, ct);
                        buffer = Optimal.ArrayPool.Rent(bufferSize);
                    }
                    else
                    {
                        int padding = (sectorSize - (read % sectorSize)) % sectorSize;
                        int totalSize = read + padding;

                        byte[] dataToSend = Optimal.ArrayPool.Rent(totalSize);
                        Buffer.BlockCopy(buffer, 0, dataToSend, 0, read);

                        await writer.WriteAsync(new DataChunk { Buffer = dataToSend, ValidLength = totalSize }, ct);
                    }
                }
            }
            finally
            {
                Optimal.ArrayPool.Return(buffer);
                writer.Complete();
            }
        }

        private async Task ConsumeDataAsync(ChannelReader<DataChunk> reader, FileStream physicalDriveStream, long remainingBytes, long headerSize, long totalBytes, CancellationToken ct)
        {
            long totalWritten = 0;

            await foreach (var chunk in reader.ReadAllAsync(ct))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    long bytesToWriteFromBuffer = Math.Min(chunk.ValidLength, remainingBytes - totalWritten);

                    await physicalDriveStream.WriteAsync(chunk.Buffer.AsMemory(0, (int)bytesToWriteFromBuffer), ct);
                    totalWritten += bytesToWriteFromBuffer;

                    long overallWritten = headerSize + totalWritten;

                    _progressReporter.ReportProgressWithInterval(overallWritten, totalBytes, $"{WorkTitle} 진행중...", 1.0);
                }
                catch (IOException)
                {
                    int errorCode = Marshal.GetLastWin32Error();

                    throw new Win32Exception(errorCode == 0 ? -1 : errorCode, $"{WorkTitle} 실패 (오류코드: {errorCode})");
                }
                finally
                {
                    Optimal.ArrayPool.Return(chunk.Buffer);
                }

                if (totalWritten >= remainingBytes)
                    break;
            }
            physicalDriveStream.Flush();
        }
    }
}