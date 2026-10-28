using PickPack.Disk.Interfaces;
using PickPack.Disk.IO;

namespace PickPack.Disk.Services
{
    public class UrlWriteHandler : IImageWriteHandler
    {
        readonly Action<int, string, string?> _progressCallback;
        readonly HttpClient _httpClient;

        public UrlWriteHandler(Action<int, string, string?> progressCallback)
        {
            _progressCallback = progressCallback;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(180)
            };
        }

        public async Task<(Stream stream, long length)> OpenStreamAsync(string url, CancellationToken ct)
        {
            _progressCallback(0, "파일 정보 확인 중...", url);

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            var headResponse = await _httpClient.SendAsync(headRequest, ct);

            headResponse.EnsureSuccessStatusCode();

            long? contentLength = headResponse.Content.Headers.ContentLength;

            if (!contentLength.HasValue)
                throw new InvalidOperationException("서버에서 파일 크기 정보를 제공하지 않습니다.");

            _progressCallback(0, "다운로드 시작 중...", $"파일 크기: {contentLength.Value / (1024 * 1024)} MB");

            var downloadStream = await CreateDownloadStreamAsync(url, contentLength.Value, ct);

            return (downloadStream, contentLength.Value);
        }

        private async Task<Stream> CreateDownloadStreamAsync(string url, long contentLength, CancellationToken ct)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync(ct);
            var progressStream = new ProgressTrackingStream(responseStream, contentLength, _progressCallback, response);

            return progressStream;
        }

        public void Dispose() => _httpClient?.Dispose();
    }
}