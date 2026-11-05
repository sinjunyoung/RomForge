using Common.Models;
using NSW.HacPack.IO;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class StreamingRomfsSectionWriter
{
    private const int BlockSize = IvfcConstants.HashBlockSize;

    public static MemoryStream[] Write(Stream ncaFile, int compressLevel, ref NcaHeader ncaHeader, int sectionIndex, NcaGenerationOptions settings, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ivfcStreams = new MemoryStream[5];

        for (int i = 0; i < 5; i++)
            ivfcStreams[i] = new MemoryStream();

        try
        {
            using var section = EncryptedSectionStream.Create(ncaFile, ref ncaHeader, sectionIndex, settings);
            long dataStart = section.Position;
            CompressedRomfsInfo info;

            section.BeginIvfc();

            using (var source = settings.OpenRomfsImage())
                info = CompressedRomfsWriter.Compress(source, section, compressLevel, progress, ct);

            long dataSize = section.Position - dataStart;

            NcaHeaderService.SetCompressionInfo(ref ncaHeader, sectionIndex, info.TableOffset, info.TableSize, info.TableHeader);
            NcaHeaderService.SetIvfcLevel5(ref ncaHeader, sectionIndex, (ulong)dataSize);
            section.EndIvfc(ivfcStreams[4], out ulong lvlSize4);
            NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, 4, lvlSize4);

            for (int b = 3; b >= 0; b--)
            {
                ct.ThrowIfCancellationRequested();
                IvfcGenerator.GenerateHashLevel(ivfcStreams[b], ivfcStreams[b + 1], out ulong lvlSize);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, b, lvlSize);
            }

            long hashLevelsStart = (dataSize + BlockSize - 1) / BlockSize * BlockSize;

            section.Write(new byte[hashLevelsStart - dataSize]);
            NcaHeaderService.SetIvfcLogicalOffsetsDataFirst(ref ncaHeader, sectionIndex, (ulong)hashLevelsStart);

            for (int c = 0; c < 5; c++)
            {
                ct.ThrowIfCancellationRequested();

                ivfcStreams[c].Position = 0;

                ivfcStreams[c].CopyTo(section);
            }

            NcaGenerator.WritePadding(section);

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