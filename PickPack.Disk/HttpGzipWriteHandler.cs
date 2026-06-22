using ICSharpCode.SharpZipLib.GZip;

namespace PickPack.Disk
{
    public class HttpGzipWriteHandler(HttpClient httpClient, Action<int, string, string?> progressCallback, long compressedSize, long uncompressedSize) : IImageWriteHandler
    {
        public async Task<(Stream stream, long length)> OpenStreamAsync(string imageUrl, CancellationToken cancellationToken)
        {
            progressCallback(0, "이미지 다운로드 시작...", string.Empty);

            var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var progressStream = new ProgressReportingStream(httpStream, compressedSize, progressCallback);
            var gzipStream = new GZipInputStream(progressStream);
            var wrapperStream = new CompositeDisposableStream(gzipStream, new MultiDisposable(progressStream, httpStream, response));

            return (wrapperStream, uncompressedSize);
        }

        private class ProgressReportingStream(Stream baseStream, long totalBytes, Action<int, string, string?> progressCallback) : Stream
        {
            private long bytesRead;
            private int lastReportedPercent = -1;
            private DateTime lastReportTime = DateTime.MinValue;

            public override bool CanRead => baseStream.CanRead;
            public override bool CanSeek => baseStream.CanSeek;
            public override bool CanWrite => baseStream.CanWrite;
            public override long Length => baseStream.Length;
            public override long Position
            {
                get => baseStream.Position;
                set => baseStream.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = baseStream.Read(buffer, offset, count);

                ReportProgress(read);

                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int read = await baseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

                ReportProgress(read);

                return read;
            }

            private void ReportProgress(int bytesJustRead)
            {
                bytesRead += bytesJustRead;

                if (totalBytes <= 0)
                    return;

                int currentPercent = (int)(bytesRead * 100 / totalBytes);

                var now = DateTime.UtcNow;

                if (currentPercent != lastReportedPercent || (now - lastReportTime).TotalSeconds >= 1.0)
                {
                    lastReportedPercent = currentPercent;
                    lastReportTime = now;

                    double downloadedMB = bytesRead / 1024.0 / 1024.0;
                    double totalMB = totalBytes / 1024.0 / 1024.0;

                    progressCallback(currentPercent, "이미지 다운로드 중...", $"{downloadedMB:F1} MB / {totalMB:F1} MB");
                }
            }

            public override void Flush() => baseStream.Flush();
            public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
            public override void SetLength(long value) => baseStream.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => baseStream.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    baseStream?.Dispose();

                base.Dispose(disposing);
            }
        }

        private class MultiDisposable(params IDisposable[] disposables) : IDisposable
        {
            public void Dispose()
            {
                foreach (var d in disposables)
                {
                    try { d?.Dispose(); } catch { }
                }
            }
        }
    }
}