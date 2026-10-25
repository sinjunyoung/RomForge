using CD.Core.Constants;
using CD.Core.Models;
using Common;

namespace CD.Core.Services.Writers;

public static class CookedIsoWriter
{
    private const int UserDataSize = 2048;
    private const int ChunkBufferSize = 4 << 20;

    public static bool CanWrite(DiscImage image) => image.Tracks.Count(IsDataTrack) == 1;

    public static async Task<string> WriteAsync(DiscImage image, string outputDir, string outputBaseName, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var dataTracks = image.Tracks.Where(IsDataTrack).ToList();

        if (dataTracks.Count != 1)
            throw new NotSupportedException($"ISO 변환은 데이터 트랙이 1개인 이미지에서만 가능합니다(현재 데이터 트랙 수: {dataTracks.Count}).");

        var track = dataTracks[0];
        var userDataOffset = GetUserDataOffset(track);

        Directory.CreateDirectory(outputDir);

        var isoPath = Utils.GetUniqueFilePath(Path.Combine(outputDir, $"{outputBaseName}.iso"));

        try
        {
            await using var outStream = new FileStream(isoPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            await using var src = track.OpenSectorStream();

            await SkipAsync(src, (long)track.PregapSectors * track.SourceSectorSize, ct);
            await CopyUserDataAsync(src, outStream, track, userDataOffset, outputBaseName, progress, ct);
        }
        catch
        {
            if (File.Exists(isoPath))
                File.Delete(isoPath);

            throw;
        }

        return isoPath;
    }

    private static async Task CopyUserDataAsync(Stream src, Stream dst, DiscTrack track, int userDataOffset, string label, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sourceSectorSize = track.SourceSectorSize;
        var sectorsPerChunk = Math.Max(1, ChunkBufferSize / sourceSectorSize);
        var srcBuffer = new byte[sectorsPerChunk * sourceSectorSize];
        var dstBuffer = new byte[sectorsPerChunk * UserDataSize];
        var totalSectors = track.LengthSectors;
        var remaining = totalSectors;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            var chunkSectors = Math.Min(sectorsPerChunk, remaining);

            await ReadExactAsync(src, srcBuffer, chunkSectors * sourceSectorSize, ct);

            for (var i = 0; i < chunkSectors; i++)
                Buffer.BlockCopy(srcBuffer, i * sourceSectorSize + userDataOffset, dstBuffer, i * UserDataSize, UserDataSize);

            await dst.WriteAsync(dstBuffer.AsMemory(0, chunkSectors * UserDataSize), ct);

            remaining -= chunkSectors;

            progress?.Report(new ProgressInfo((int)((long)(totalSectors - remaining) * 100 / totalSectors), "ISO 변환 중", label, string.Empty, string.Empty));
        }
    }

    private static async Task SkipAsync(Stream stream, long bytes, CancellationToken ct)
    {
        if (bytes <= 0)
            return;

        var buffer = new byte[(int)Math.Min(bytes, ChunkBufferSize)];

        while (bytes > 0)
        {
            ct.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(bytes, buffer.Length)), ct);

            if (read == 0)
                throw new EndOfStreamException("트랙 데이터를 읽는 도중 스트림이 예상보다 일찍 끝났습니다.");

            bytes -= read;
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

    private static bool IsDataTrack(DiscTrack track) => !string.Equals(track.DataType, CueFormatStrings.Audio, StringComparison.OrdinalIgnoreCase);

    private static int GetUserDataOffset(DiscTrack track)
    {
        var dataType = track.DataType.ToUpperInvariant();

        var offset = track.SourceSectorSize switch
        {
            2048 => 0,
            2336 => 8,
            _ => dataType switch
            {
                CueFormatStrings.Mode1_2352 => 16,
                CueFormatStrings.Mode2_2352 => 24,
                CueFormatStrings.Cdi_2352 => 24,
                _ => throw new NotSupportedException($"ISO 변환을 지원하지 않는 트랙 타입입니다: {track.DataType}")
            }
        };

        if (track.SourceSectorSize < offset + UserDataSize)
            throw new InvalidDataException($"섹터 크기({track.SourceSectorSize})가 {track.DataType} 트랙의 사용자 데이터를 담기에 작습니다.");

        return offset;
    }
}