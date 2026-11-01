using Common;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class UncompressedRomfsSectionWriter
{
    private const int BlockSize = IvfcConstants.HashBlockSize;
    private const int HashSize = 32;
    private const int CopyBufferSize = 0x800000;

    public static MemoryStream[] Write(Stream ncaFile, ref NcaHeader ncaHeader, int sectionIndex, NcaGenerationOptions settings, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ivfcStreams = new MemoryStream[5];

        for (int i = 0; i < 5; i++)
            ivfcStreams[i] = new MemoryStream();

        try
        {
            using var source = settings.RomfsImageOpener!();

            long hashedSize = source.Length;
            long dataSize = source is IUnpaddedLengthSource unpadded ? unpadded.UnpaddedLength : hashedSize;
            long[] expectedSizes = GetLevelSizes(hashedSize);
            long levelsStart = ncaFile.Position;

            WriteZeros(ncaFile, expectedSizes.Sum(), ct);

            long dataStart = ncaFile.Position;

            CopyData(source, ncaFile, hashedSize, progress, ct);

            long dataEnd = ncaFile.Position;

            NcaHeaderService.SetIvfcLevel5(ref ncaHeader, sectionIndex, (ulong)dataSize);

            using (var region = new ReadOnlyRegionStream(ncaFile, dataStart, hashedSize))
            {
                IvfcGenerator.GenerateHashLevel(ivfcStreams[4], region, out ulong lvlSize4);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, 4, lvlSize4);
            }

            for (int b = 3; b >= 0; b--)
            {
                ct.ThrowIfCancellationRequested();
                IvfcGenerator.GenerateHashLevel(ivfcStreams[b], ivfcStreams[b + 1], out ulong lvlSize);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, b, lvlSize);
                progress.ReportStatus($"IVFC 생성 중... {4 - b}/5", -1);
            }

            for (int c = 0; c < 5; c++)
            {
                if (ivfcStreams[c].Length != expectedSizes[c])
                    throw new InvalidDataException($"IVFC 레벨 {c} 크기가 예상과 다릅니다: {ivfcStreams[c].Length} / {expectedSizes[c]}");
            }

            NcaHeaderService.SetIvfcLogicalOffsets(ref ncaHeader, sectionIndex);

            ncaFile.Position = levelsStart;

            for (int c = 0; c < 5; c++)
            {
                ct.ThrowIfCancellationRequested();

                ivfcStreams[c].Position = 0;
                ivfcStreams[c].CopyTo(ncaFile);
            }

            ncaFile.Position = dataEnd;

            NcaGenerator.WritePadding(ncaFile);

            return ivfcStreams;
        }
        catch
        {
            foreach (var s in ivfcStreams)
                s.Dispose();

            throw;
        }
    }

    private static long[] GetLevelSizes(long dataSize)
    {
        var sizes = new long[5];
        long input = dataSize;

        for (int level = 4; level >= 0; level--)
        {
            long blocks = (input + BlockSize - 1) / BlockSize;
            long hashBytes = blocks * HashSize;

            sizes[level] = (hashBytes + BlockSize - 1) / BlockSize * BlockSize;
            input = sizes[level];
        }

        return sizes;
    }

    private static void WriteZeros(Stream output, long count, CancellationToken ct)
    {
        var zeros = new byte[Math.Min(count, 1 << 20)];

        while (count > 0)
        {
            ct.ThrowIfCancellationRequested();

            int chunk = (int)Math.Min(zeros.Length, count);

            output.Write(zeros, 0, chunk);

            count -= chunk;
        }
    }

    private static void CopyData(Stream source, Stream output, long expectedSize, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var reporter = new ProgressReporter("RomFS 기록 중", string.Empty, expectedSize, progress);
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        long copied = 0;

        try
        {
            int read;

            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                output.Write(buffer, 0, read);

                copied += read;

                reporter.AddProgress(read);
            }

            reporter.ForceReport();
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        if (copied != expectedSize)
            throw new InvalidDataException($"romfs 데이터 길이가 예상과 다릅니다: {copied} / {expectedSize}");
    }
}