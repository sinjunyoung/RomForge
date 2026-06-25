using PBP.Core.Models;

namespace PBP.Core.Services;

public record DiscWriteInfo(Stream IsoStream, long IsoLength, string GameId, string GameTitle, byte[] TocData);

public static class PsarDiscWriter
{
    private const int BlockSize = 0x9300;
    private const int BufferSize = 1048576;

    public static void WriteDisc(Stream outputStream, Stream isoStream, long isoLength, string gameId, string gameTitle, byte[] tocData, byte[]? configData, uint psarOffset, bool isMultiDisc, int compressionLevel, CancellationToken cancellationToken, Action<long, long>? onProgress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isoPosition = (uint)(outputStream.Position - psarOffset);
        var actualIsoSize = (uint)isoLength;
        var isoSize = actualIsoSize;
        uint p1Offset;
        uint p2Offset = 0;

        if ((isoSize % BlockSize) != 0)
            isoSize += (BlockSize - (isoSize % BlockSize));

        outputStream.Write("PSISOIMG0000", 0, 12);
        p1Offset = (uint)outputStream.Position;
        outputStream.WriteUInt32(isoSize + 0x100000, 1);
        outputStream.WriteInt32(0, 0xFC);

        var data1 = (byte[])PbpTemplateProvider.GetBaseHeaderTemplate().Clone();
        var idBytes = System.Text.Encoding.ASCII.GetBytes(gameId);

        Array.Copy(idBytes, 0, data1, 1, 4);
        Array.Copy(idBytes, 4, data1, 6, 5);

        if (configData != null && configData.Length > 0)
            Array.Copy(configData, 0, data1, 0x20, configData.Length);

        if (tocData == null || tocData.Length == 0)
            throw new Exception("Invalid TOC");

        Array.Copy(tocData, 0, data1, 1024, tocData.Length);
        outputStream.Write(data1, 0, data1.Length);

        if (isMultiDisc)
            outputStream.WriteInt32(0, 1);
        else
        {
            p2Offset = (uint)outputStream.Position;
            outputStream.WriteUInt32(isoSize + 0x100000 + 0x2d31, 1);
        }

        var data2 = (byte[])PbpTemplateProvider.GetBaseFooterTemplate().Clone();
        var titleBytes = System.Text.Encoding.ASCII.GetBytes(gameTitle);

        Array.Clear(data2, 8, 128);
        Array.Copy(titleBytes, 0, data2, 8, gameTitle.Length);
        data2[8 + titleBytes.Length] = 0;

        outputStream.Write(data2, 0, data2.Length);

        var indexOffset = (uint)outputStream.Position;
        uint offset = 0;
        uint x = compressionLevel == 0 ? (uint)BlockSize : 0;
        var dummy = new uint[6];

        for (var i = 0; i < isoSize / BlockSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            outputStream.WriteUInt32(offset, 1);
            outputStream.WriteUInt32(x, 1);
            outputStream.Write(dummy, 0, sizeof(uint) * dummy.Length);

            if (compressionLevel == 0)
                offset += BlockSize;
        }

        offset = (uint)outputStream.Position;

        for (var i = 0; i < (isoPosition + psarOffset + 0x100000) - offset; i++)
            outputStream.WriteByte(0);

        uint curSize = 0, totSize = 0;

        if (compressionLevel == 0)
        {
            var buffer = new byte[BufferSize];
            int bytesRead;

            while ((bytesRead = isoStream.Read(buffer, 0, BufferSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                outputStream.Write(buffer, 0, bytesRead);
                totSize += (uint)bytesRead;
                curSize += (uint)bytesRead;

                onProgress?.Invoke(curSize, actualIsoSize);
            }

            for (var i = 0; i < (isoSize - actualIsoSize); i++)
                outputStream.WriteByte(0);
        }
        else
        {
            var indexes = new IsoIndexHeader[isoSize / BlockSize];
            var idx = 0;

            offset = 0;

            int bytesRead;
            var readBuffer = new byte[BlockSize];
            var compressedBuffer = new byte[BufferSize];

            while ((bytesRead = isoStream.Read(readBuffer, 0, BlockSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                totSize += (uint)bytesRead;
                curSize += (uint)bytesRead;
                onProgress?.Invoke(curSize, actualIsoSize);

                if (bytesRead < BlockSize)
                    Array.Clear(readBuffer, bytesRead, BlockSize - bytesRead);

                var compressedSize = (uint)Compression.Compress(readBuffer, compressedBuffer, compressionLevel);

                indexes[idx] = new IsoIndexHeader { Offset = offset };

                if (compressedSize >= BlockSize)
                {
                    indexes[idx].Length = BlockSize;
                    outputStream.Write(readBuffer, 0, BlockSize);
                    offset += BlockSize;
                }
                else
                {
                    indexes[idx].Length = compressedSize;
                    outputStream.Write(compressedBuffer, 0, (int)compressedSize);
                    offset += compressedSize;
                }

                idx++;
            }

            if (idx != isoSize / BlockSize)
                throw new Exception("Some error happened.");

            offset = (uint)outputStream.Position;
            uint endOffset = 0;

            if (!isMultiDisc)
            {
                if ((offset % 0x10) != 0)
                {
                    endOffset = offset + (0x10 - (offset % 0x10));

                    for (var block = 0; block < (endOffset - offset); block++)
                        outputStream.WriteByte((byte)'0');
                }
                else
                    endOffset = offset;

                endOffset -= psarOffset;
            }

            offset = (uint)outputStream.Position;

            if (!isMultiDisc)
            {
                outputStream.Seek(p1Offset, SeekOrigin.Begin);
                outputStream.WriteUInt32(endOffset, 1);

                endOffset += 0x2d31;
                outputStream.Seek(p2Offset, SeekOrigin.Begin);
                outputStream.WriteUInt32(endOffset, 1);
            }

            outputStream.Seek(indexOffset, SeekOrigin.Begin);
            outputStream.Write(indexes, 0, (int)(4 + 4 + (6 * 4) * (isoSize / BlockSize)));

            outputStream.Seek(offset, SeekOrigin.Begin);
        }
    }
}