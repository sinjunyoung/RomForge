namespace N3DS.Core.Services;

public static class Extensions
{
    public static async Task CopyToAsync(this Stream source, Stream destination, long length, long totalBytes = 0, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        byte[] buffer = new byte[81920];
        long remaining = length;
        long currentWritten = totalBytes;        
        long totalSize = totalBytes > 0 ? totalBytes : length;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);

            if (read == 0)
                throw new EndOfStreamException();

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);

            remaining -= read;
            currentWritten += read;

            progress?.Invoke(currentWritten, totalSize);
        }
    }
}