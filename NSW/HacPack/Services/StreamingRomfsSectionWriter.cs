using Common;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class StreamingRomfsSectionWriter
{
    private const int BlockSize = IvfcConstants.HashBlockSize;

    public static MemoryStream[] Write(FileStream ncaFile, int compressLevel, ref NcaHeader ncaHeader, int sectionIndex, NcaGenerationOptions settings, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ivfcStreams = new MemoryStream[5];

        for (int i = 0; i < 5; i++)
            ivfcStreams[i] = new MemoryStream();

        try
        {
            long dataStart = ncaFile.Position;
            CompressedRomfsInfo info;

            using (var source = settings.RomfsImageOpener!())
                info = CompressedRomfsWriter.Compress(source, ncaFile, compressLevel, progress, ct);

            long dataSize = ncaFile.Position - dataStart;

            NcaHeaderService.SetCompressionInfo(ref ncaHeader, sectionIndex, info.TableOffset, info.TableSize, info.TableHeader);
            NcaHeaderService.SetIvfcLevel5(ref ncaHeader, sectionIndex, (ulong)dataSize);

            long dataEnd = ncaFile.Position;

            using (var region = new ReadOnlyRegionStream(ncaFile, dataStart, dataSize))
            {
                IvfcGenerator.GenerateHashLevel(ivfcStreams[4], region, out ulong lvlSize4);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, 4, lvlSize4);
            }

            ncaFile.Position = dataEnd;

            for (int b = 3; b >= 0; b--)
            {
                ct.ThrowIfCancellationRequested();
                IvfcGenerator.GenerateHashLevel(ivfcStreams[b], ivfcStreams[b + 1], out ulong lvlSize);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, b, lvlSize);
            }

            long hashLevelsStart = (dataSize + BlockSize - 1) / BlockSize * BlockSize;

            ncaFile.Write(new byte[hashLevelsStart - dataSize]);

            NcaHeaderService.SetIvfcLogicalOffsetsDataFirst(ref ncaHeader, sectionIndex, (ulong)hashLevelsStart);

            for (int c = 0; c < 5; c++)
            {
                ct.ThrowIfCancellationRequested();

                ivfcStreams[c].Position = 0;

                ivfcStreams[c].CopyTo(ncaFile);
            }

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
}