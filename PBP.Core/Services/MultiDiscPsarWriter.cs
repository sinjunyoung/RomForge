using PBP.Core.Models;

namespace PBP.Core.Services;

public static class MultiDiscPsarWriter
{
    public static void WritePsar(Stream outputStream, string mainGameTitle, string mainGameId, IReadOnlyList<DiscWriteInfo> discs, uint psarOffset, int compressionLevel, CancellationToken cancellationToken, Action<long, long>? onProgress = null)
    {
        var isoPositions = new uint[5];
        uint x, endOffset;

        outputStream.Write("PSTITLEIMG000000", 0, 16);

        var p1Offset = (uint)outputStream.Position;

        outputStream.WriteInt32(0, 2);
        outputStream.WriteInt32(0x2CC9C5BC, 1);
        outputStream.WriteInt32(0x33B5A90F, 1);
        outputStream.WriteInt32(0x06F6B4B3, 1);
        outputStream.WriteUInt32(0xB25945BA, 1);

        outputStream.WriteInt32(0, 0x76);

        var mOffset = (uint)outputStream.Position;

        outputStream.Write(isoPositions, 1, sizeof(uint) * 5);

        outputStream.WriteRandom(12);
        outputStream.WriteInt32(0, 8);

        outputStream.Write('_');
        outputStream.Write(mainGameId, 0, 4);
        outputStream.Write('_');
        outputStream.Write(mainGameId, 4, 5);

        outputStream.WriteChar(0, 0x15);

        var p2Offset = (uint)outputStream.Position;
        outputStream.WriteInt32(0, 2);

        outputStream.Write(PbpTemplates.Data3, 0, PbpTemplates.Data3.Length);

        outputStream.Write(mainGameTitle, 0, mainGameTitle.Length);
        outputStream.WriteChar(0, 0x80 - mainGameTitle.Length);

        outputStream.WriteInt32(7, 1);
        outputStream.WriteInt32(0, 0x1C);

        var totalBytes = discs.Sum(d => d.IsoLength);
        long completedBytes = 0;

        for (var discNo = 0; discNo < discs.Count; discNo++)
        {
            var disc = discs[discNo];

            var offset = (uint)outputStream.Position;

            if (offset % 0x8000 > 0)
            {
                x = 0x8000 - (offset % 0x8000);
                outputStream.WriteChar(0, (int)x);
            }

            isoPositions[discNo] = (uint)(outputStream.Position - psarOffset);

            PsarDiscWriter.WriteDisc(outputStream, disc.IsoStream, disc.IsoLength, disc.GameId, disc.GameTitle, disc.TocData, psarOffset, true, compressionLevel, cancellationToken, (cur, _) => onProgress?.Invoke(completedBytes + cur, totalBytes));

            if (cancellationToken.IsCancellationRequested)
                return;
        }

        x = (uint)outputStream.Position;

        if ((x % 0x10) != 0)
        {
            endOffset = x + (0x10 - (x % 0x10));

            for (var i = 0; i < (endOffset - x); i++)
                outputStream.WriteByte((byte)'0');
        }
        else
            endOffset = x;

        endOffset -= psarOffset;

        var finalOffset = (uint)outputStream.Position;

        outputStream.Seek(p1Offset, SeekOrigin.Begin);
        outputStream.WriteUInt32(endOffset, 1);

        endOffset += 0x2d31;
        outputStream.Seek(p2Offset, SeekOrigin.Begin);
        outputStream.WriteUInt32(endOffset, 1);

        outputStream.Seek(mOffset, SeekOrigin.Begin);
        outputStream.Write(isoPositions, 1, sizeof(uint) * isoPositions.Length);

        outputStream.Seek(finalOffset, SeekOrigin.Begin);
    }
}