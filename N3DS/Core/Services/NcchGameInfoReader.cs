using N3DS.Core.FileSystem;
using N3DS.Core.Models;
using System.Buffers.Binary;
using System.Text;

namespace N3DS.Core.Services;

public static class NcchGameInfoReader
{
    private const int MediaUnit = 0x200;

    public static async Task<SmdhInfo?> LoadAsync(Stream ncchStream, CancellationToken ct = default)
    {
        byte[] ncchHeaderBuf = new byte[0x200];

        ncchStream.Position = 0;

        await ncchStream.ReadExactlyAsync(ncchHeaderBuf, 0, 0x200, ct);

        var ncch = NcchHeader.Parse(ncchHeaderBuf, 0);

        if (ncch.ExefsOffset == 0)
            return null;

        long exefsOffset = (long)ncch.ExefsOffset * MediaUnit;
        byte[] exefsHeader = new byte[0x200];

        ncchStream.Position = exefsOffset;

        await ncchStream.ReadExactlyAsync(exefsHeader, 0, 0x200, ct);

        for (int i = 0; i < 8; i++)
        {
            int entryBase = i * 16;
            string name = Encoding.ASCII.GetString(exefsHeader, entryBase, 8).TrimEnd('\0');

            if (name != "icon")
                continue;

            uint fileOffset = BinaryPrimitives.ReadUInt32LittleEndian(exefsHeader.AsSpan(entryBase + 8, 4));
            uint fileSize = BinaryPrimitives.ReadUInt32LittleEndian(exefsHeader.AsSpan(entryBase + 12, 4));

            if (fileSize == 0)
                return null;

            byte[] smdhData = new byte[fileSize];

            ncchStream.Position = exefsOffset + 0x200 + fileOffset;

            await ncchStream.ReadExactlyAsync(smdhData, 0, (int)fileSize, ct);
            return SmdhParser.TryParse(smdhData);
        }

        return null;
    }
}