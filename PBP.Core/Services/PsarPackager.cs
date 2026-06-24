namespace PBP.Core.Services;

public static class PsarPackager
{
    public static void WritePsar(Stream outputStream, string mainGameTitle, string mainGameId, IReadOnlyList<DiscWriteInfo> discs, uint psarOffset, int compressionLevel, CancellationToken cancellationToken, Action<long, long>? onProgress = null)
    {
        var isoPositions = new uint[5];
        byte[] zeroBuffer = new byte[0x8000];

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
        outputStream.Write(PbpTemplateProvider.GetSystemConfigTemplate(), 0, PbpTemplateProvider.GetSystemConfigTemplate().Length);
        outputStream.Write(mainGameTitle, 0, mainGameTitle.Length);
        outputStream.WriteChar(0, 0x80 - mainGameTitle.Length);
        outputStream.WriteInt32(7, 1);
        outputStream.WriteInt32(0, 0x1C);

        bool isMulti = discs.Count > 1;
        var totalBytes = discs.Sum(d => d.IsoLength);
        long completedBytes = 0;

        for (var discNo = 0; discNo < discs.Count; discNo++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var disc = discs[discNo];
            var offset = (uint)outputStream.Position;

            if (offset % 0x8000 > 0)
            {
                uint pad = 0x8000 - (offset % 0x8000);
                outputStream.Write(zeroBuffer, 0, (int)pad);
            }

            isoPositions[discNo] = (uint)(outputStream.Position - psarOffset);

            PsarDiscWriter.WriteDisc(outputStream, disc.IsoStream, disc.IsoLength, disc.GameId, disc.GameTitle, disc.TocData, psarOffset, isMulti, compressionLevel, cancellationToken, (cur, _) => onProgress?.Invoke(completedBytes + cur, totalBytes));

            completedBytes += disc.IsoLength;
        }

        uint x = (uint)outputStream.Position;
        uint endOffset = (x % 0x10 != 0) ? x + (0x10 - (x % 0x10)) : x;
        int padCount = (int)(endOffset - x);

        if (padCount > 0) 
            outputStream.Write([.. new byte[padCount].Select(_ => (byte)'0')], 0, padCount);

        uint finalOffset = (uint)outputStream.Position;

        endOffset -= psarOffset;
        outputStream.Seek(p1Offset, SeekOrigin.Begin);
        outputStream.WriteUInt32(endOffset, 1);
        outputStream.Seek(p2Offset, SeekOrigin.Begin);
        outputStream.WriteUInt32(endOffset + 0x2d31, 1);
        outputStream.Seek(mOffset, SeekOrigin.Begin);
        outputStream.Write(isoPositions, 1, sizeof(uint) * 5);
        outputStream.Seek(finalOffset, SeekOrigin.Begin);
    }
}