using Ionic.Zlib;
using PickPack.Disk.ETC;

namespace PickPack.Disk
{
    public class ImageReader
    {
        #region Field

        readonly ProgressReporter _progressReporter;
        CancellationToken _ct;

        #endregion

        #region Constructor

        public ImageReader()
        {
            _progressReporter = new ProgressReporter();
            _progressReporter.ProgressChanged += (sender, args) => OnProgressChanged(args.Percent, args.Message1, args.Message2);
        }

        #endregion

        #region Event

        public event EventHandler<ProgressEventArgs>? ProgressChanged;
        public event EventHandler<EventArgs>? WriteEnded;

        protected virtual void OnProgressChanged(int percent, string message1, string? message2 = "")
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Percent = percent,
                Message1 = message1,
                Message2 = message2
            });
        }

        protected virtual void OnWriteEnded()
        {
            WriteEnded?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        public async Task ReadImageAsync(int physicalDriveNumber, string outputPath, long maxOutputSegmentSize64, CompressionLevel compressionLevel, CancellationToken ct)
        {
            try
            {
                _ct = ct;
                _progressReporter.Initialize(ct);

                string extension = Path.GetExtension(outputPath).ToLowerInvariant();

                if (!ImageReaderFactory.IsSupported(extension))
                    throw new InvalidOperationException($"지원하지 않는 파일 형식입니다. 지원 형식: {string.Join(", ", ImageReaderFactory.GetSupportedExtensions())}");

                string physicalDrivePath = $@"\\.\PHYSICALDRIVE{physicalDriveNumber}";
                long driveSize = DiskUtil.GetDiskLength(physicalDriveNumber);
                using var driveStream = new FileStream(physicalDrivePath, FileMode.Open, FileAccess.Read, FileShare.None, Optimal.BufferSize, FileOptions.Asynchronous);
                var handler = ImageReaderFactory.GetHandler(extension, maxOutputSegmentSize64, compressionLevel, _progressReporter.ReportProgress, ct)
                    ?? throw new InvalidOperationException($"지원하지 않는 파일 형식입니다: {extension}");

                await handler.WriteImageAsync(driveStream, outputPath, driveSize, ct);

                ct.ThrowIfCancellationRequested();
                
                _progressReporter.ReportCompletion("이미지 저장 완료");

                OnWriteEnded();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"오류: {ex.Message}", ex);
            }
        }
    }
}