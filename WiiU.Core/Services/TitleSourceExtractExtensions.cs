namespace WiiU.Core.Services;

public static class TitleSourceExtractExtensions
{
    private const int BufferSize = 1024 * 1024;

    public static void ExtractTo(this ITitleSource source, string destinationFolder, Action<long, long, string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        var paths = source.EnumerateFiles().ToList();
        long total = 0;

        foreach (string path in paths)
            total += source.GetFileSize(path);

        var progress = new ByteProgress(total, onProgress);
        var buffer = new byte[BufferSize];

        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destPath = Path.Combine(destinationFolder, path.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            progress.Advance(0, path);

            using var outStream = File.Create(destPath);
            using var inStream = source.OpenRead(path);
            int read;

            while ((read = inStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                outStream.Write(buffer, 0, read);
                progress.Advance(read, path);
            }
        }

        progress.Complete(paths.Count > 0 ? paths[^1] : string.Empty);
    }
}