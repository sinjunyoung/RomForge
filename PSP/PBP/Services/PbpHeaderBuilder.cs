using PSP.PBP.Models;

namespace PSP.PBP.Services;

public static class PbpHeaderBuilder
{
    private const uint PBPMAGIC = 0x50425000;

    public static void EnsureRequiredAssets(PbpAssets assets, byte[] basePbpBytes)
    {
        using var basePbp = new MemoryStream(basePbpBytes);
        var baseHeader = new uint[10];

        basePbp.Read(baseHeader, 10);

        if (baseHeader[0] != PBPMAGIC)
            throw new Exception("BASE.PBP is not a valid PBP file.");

        if (assets.Icon0Png is null)
        {
            var icon0Size = baseHeader[4] - baseHeader[3];
            var icon0Buffer = new byte[icon0Size];

            basePbp.Seek(baseHeader[3], SeekOrigin.Begin);
            basePbp.Read(icon0Buffer, 0, (int)icon0Size);

            assets.Icon0Png = icon0Buffer;
        }

        if (assets.DataPsp is null)
        {
            var pspHeader = new uint[12];

            basePbp.Seek(baseHeader[8], SeekOrigin.Begin);
            basePbp.Read(pspHeader, 12);

            var prxSize = pspHeader[11];

            basePbp.Seek(baseHeader[8], SeekOrigin.Begin);

            var dataPspBuffer = new byte[prxSize];

            basePbp.Read(dataPspBuffer, 0, (int)prxSize);

            assets.DataPsp = dataPspBuffer;
        }
    }

    public static uint[] BuildHeader(PbpAssets assets, uint sfoSize)
    {
        uint currentOffset = 0x28;
        var header = new uint[0x28 / 4];

        header[0] = PBPMAGIC;
        header[1] = 0x10000;
        header[2] = currentOffset;

        currentOffset += sfoSize;
        header[3] = currentOffset;

        currentOffset += (uint)(assets.Icon0Png?.Length ?? 0);
        header[4] = currentOffset;

        currentOffset += (uint)(assets.Icon1Pmf?.Length ?? 0);
        header[5] = currentOffset;

        currentOffset += (uint)(assets.Pic0Png?.Length ?? 0);
        header[6] = currentOffset;

        currentOffset += (uint)(assets.Pic1Png?.Length ?? 0);
        header[7] = currentOffset;

        currentOffset += (uint)(assets.Snd0At3?.Length ?? 0);
        header[8] = currentOffset;

        var psarOffset = header[8] + (uint)(assets.DataPsp?.Length ?? 0);

        if ((psarOffset % 0x10000) != 0)
            psarOffset += (0x10000 - (psarOffset % 0x10000));

        header[9] = psarOffset;

        return header;
    }
}