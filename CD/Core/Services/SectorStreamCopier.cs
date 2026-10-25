namespace CD.Core.Services;

internal static class SectorStreamCopier
{
    private const int BufferSize = 4 << 20;

    public static async Task CopySectorsAsync(Stream source, Stream destination, int sectorCount, int sourceSectorSize, int outputSectorSize, Action<int>? onSectorsWritten, CancellationToken ct=default)
    {
        if (sourceSectorSize == outputSectorSize)
        {
            await CopyRawAsync(source, destination, (long)sectorCount * sourceSectorSize, sourceSectorSize, onSectorsWritten, ct);
            return;
        }

        var sectorsPerChunk = Math.Max(1, BufferSize / sourceSectorSize);
        var srcBuffer = new byte[sectorsPerChunk * sourceSectorSize];
        var dstBuffer = new byte[sectorsPerChunk * outputSectorSize];

        var remaining = sectorCount;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            var chunkSectors = Math.Min(sectorsPerChunk, remaining);

            await ReadExactAsync(source, srcBuffer, chunkSectors * sourceSectorSize, ct);

            for (var i = 0; i < chunkSectors; i++)
                Buffer.BlockCopy(srcBuffer, i * sourceSectorSize, dstBuffer, i * outputSectorSize, outputSectorSize);

            await destination.WriteAsync(dstBuffer.AsMemory(0, chunkSectors * outputSectorSize), ct);

            remaining -= chunkSectors;

            onSectorsWritten?.Invoke(chunkSectors);
        }
    }

    private static async Task CopyRawAsync(Stream source, Stream destination, long totalBytes, int sectorSize, Action<int>? onSectorsWritten, CancellationToken ct=default)
    {
        var buffer = new byte[BufferSize];
        var remaining = totalBytes;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), ct);

            if (read == 0)
                throw new EndOfStreamException("트랙 데이터를 읽는 도중 스트림이 예상보다 일찍 끝났습니다.");

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);

            remaining -= read;

            onSectorsWritten?.Invoke(read / sectorSize);
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        var read = 0;

        while (read < count)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read, count - read), ct);

            if (n == 0)
                throw new EndOfStreamException("트랙 데이터를 읽는 도중 스트림이 예상보다 일찍 끝났습니다.");

            read += n;
        }
    }
}