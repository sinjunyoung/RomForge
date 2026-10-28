using PickPack.Disk.Interfaces;

namespace PickPack.Disk.Services
{
    public static class ImageWriterFactory
    {
        private static readonly Dictionary<string, Func<Action<int, string?, string?>, IImageWriteHandler>> HandlerCreators =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { ".zip", _ => new ZipWriteHandler() },
                { ".gz", progressCallback => new GzipWriteHandler(progressCallback) },
                { ".img", _ => new ImgWriteHandler() }
            };

        public static IImageWriteHandler? GetHandler(string pathOrUrl, Action<int, string?, string?> progressCallback)
        {
            bool isUrl = IsUrl(pathOrUrl);
            string extension = Path.GetExtension(pathOrUrl);

            if (isUrl)
                return new UrlWriteHandler(progressCallback);

            return HandlerCreators.TryGetValue(extension, out var creator) ? creator(progressCallback) : null;
        }

        public static HttpGzipWriteHandler CreateHttpGzipHandler(HttpClient httpClient, Action<int, string?, string?> progressCallback, long compressedSize, long uncompressedSize) => new(httpClient, progressCallback, compressedSize, uncompressedSize);

        public static bool IsSupported(string pathOrUrl)
        {
            if (IsUrl(pathOrUrl))
                return true;

            string extension = Path.GetExtension(pathOrUrl);

            return HandlerCreators.ContainsKey(extension);
        }

        public static string[] GetSupportedExtensions()
        {
            var extensions = HandlerCreators.Keys.ToList();

            extensions.Add("URL (http/https)");

            return [.. extensions];
        }

        private static bool IsUrl(string input) => Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}