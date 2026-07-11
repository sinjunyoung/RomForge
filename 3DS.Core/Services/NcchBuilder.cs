using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public static class NcchBuilder
{
    private const int MediaUnit = 0x200;
    public const int RomFsAlign = 0x1000;

    public static async Task BuildAsync(NcchUnpackResult unpack, byte[] exefsBlock, Stream ncchSource, RomFsUnpackResult? romFs, Stream output, IRomFsFileSource? patchSource = null, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        var hdr = unpack.Header;
        long ncchSize = MediaUnit;
        long exhdrOffset = 0, exhdrSize = 0;

        if (unpack.ExHeader != null)
        {
            exhdrSize = unpack.ExHeader.Length;
            exhdrOffset = ncchSize;
            ncchSize += exhdrSize;
        }

        long logoOffset = 0, logoSize = 0;

        if (unpack.Logo != null)
        {
            logoSize = AlignUp(unpack.Logo.Length, MediaUnit);
            logoOffset = AlignUp(ncchSize, MediaUnit);
            ncchSize = logoOffset + logoSize;
        }

        long plainOffset = 0, plainSize = 0;

        if (unpack.PlainRegion != null)
        {
            plainSize = AlignUp(unpack.PlainRegion.Length, MediaUnit);
            plainOffset = AlignUp(ncchSize, MediaUnit);
            ncchSize = plainOffset + plainSize;
        }

        long exefsOffset = 0, exefsSize = 0, exefsHashSize = 0;

        if (exefsBlock.Length > 0)
        {
            exefsHashSize = AlignUp(ExeFsHeader.Size, MediaUnit);
            exefsSize = AlignUp(exefsBlock.Length, MediaUnit);
            exefsOffset = AlignUp(ncchSize, MediaUnit);
            ncchSize = exefsOffset + exefsSize;
        }

        long romfsOffset = 0, romfsSize = 0, romfsHashSize = 0;

        if (romFs != null)
        {
            Dictionary<string, long>? patchSizeMap = null;

            if (patchSource != null)
            {
                long dataBase = romFs.DataLevel2Offset + romFs.RomFsHeader.DataOffset;
                patchSizeMap = await RomFsPacker.BuildPatchSizeMapAsync(romFs.Files, patchSource, ncchSource, dataBase, ct);
            }

            var (totalSize, level0Size, _, _, _) = RomFsPacker.CalculateLayout(romFs.Directories, romFs.Files, patchSizeMap);

            long romfsHeaderSize = (long)level0Size;

            romfsHashSize = AlignUp(romfsHeaderSize, MediaUnit);
            romfsSize = AlignUp((long)totalSize, MediaUnit);
            romfsOffset = AlignUp(ncchSize, RomFsAlign);
            ncchSize = romfsOffset + romfsSize;
        }

        ncchSize = AlignUp(ncchSize, MediaUnit);

        long basePosition = output.Position;

        if (unpack.ExHeader != null)
        {
            output.Position = basePosition + exhdrOffset;

            await output.WriteAsync(unpack.ExHeader, ct);
        }

        if (unpack.Logo != null)
        {
            output.Position = basePosition + logoOffset;

            await output.WriteAsync(unpack.Logo, ct);
        }

        if (unpack.PlainRegion != null)
        {
            output.Position = basePosition + plainOffset;

            await output.WriteAsync(unpack.PlainRegion, ct);
        }

        if (exefsBlock.Length > 0)
        {
            output.Position = basePosition + exefsOffset;

            await output.WriteAsync(exefsBlock, ct);
        }

        if (romFs != null)
        {
            output.Position = basePosition + romfsOffset;
            await RomFsPacker.PackAsync(ncchSource, romFs, output, 0, patchSource, progress, ct);
        }

        byte[] ncchHdr = new byte[MediaUnit];

        hdr.Signature.CopyTo(ncchHdr, 0x000);

        ncchHdr[0x100] = (byte)'N'; ncchHdr[0x101] = (byte)'C';
        ncchHdr[0x102] = (byte)'C'; ncchHdr[0x103] = (byte)'H';

        BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x104), (uint)(ncchSize / MediaUnit));
        BinaryPrimitives.WriteUInt64LittleEndian(ncchHdr.AsSpan(0x108), hdr.PartitionId);
        BinaryPrimitives.WriteUInt16LittleEndian(ncchHdr.AsSpan(0x110), hdr.MakerCode);
        BinaryPrimitives.WriteUInt16LittleEndian(ncchHdr.AsSpan(0x112), hdr.Version);
        BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x114), hdr.FirmwareHashMask);
        BinaryPrimitives.WriteUInt64LittleEndian(ncchHdr.AsSpan(0x118), hdr.ProgramId);

        hdr.Reserved1.CopyTo(ncchHdr, 0x120);
        hdr.LogoHash.CopyTo(ncchHdr, 0x130);
        hdr.ProductCode.CopyTo(ncchHdr, 0x150);

        if (exhdrSize > 0)
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x180), hdr.ExtendedHeaderSize);

        hdr.Flags.CopyTo(ncchHdr, 0x188);
        ncchHdr[0x188 + 7] |= 0x04;
        if ((hdr.Flags[7] & 0x01) != 0)
            ncchHdr[0x188 + 7] |= 0x01;

        if (plainSize > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x190), (uint)(plainOffset / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x194), (uint)(plainSize / MediaUnit));
        }

        if (logoSize > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x198), (uint)(logoOffset / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x19C), (uint)(logoSize / MediaUnit));
        }

        if (exefsSize > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1A0), (uint)(exefsOffset / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1A4), (uint)(exefsSize / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1A8), (uint)(exefsHashSize / MediaUnit));
        }

        if (romfsSize > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1B0), (uint)(romfsOffset / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1B4), (uint)(romfsSize / MediaUnit));
            BinaryPrimitives.WriteUInt32LittleEndian(ncchHdr.AsSpan(0x1B8), (uint)(romfsHashSize / MediaUnit));
        }

        if (exhdrSize > 0)
        {
            output.Position = basePosition + exhdrOffset;

            byte[] exhdrBuf = new byte[0x400];

            await output.ReadExactlyAsync(exhdrBuf, ct);

            SHA256.HashData(exhdrBuf).CopyTo(ncchHdr, 0x160);
        }

        if (logoSize > 0)
        {
            output.Position = basePosition + logoOffset;

            byte[] logoBuf = new byte[unpack.Logo!.Length];

            await output.ReadExactlyAsync(logoBuf, ct);

            SHA256.HashData(logoBuf).CopyTo(ncchHdr, 0x130);
        }

        if (exefsSize > 0)
        {
            output.Position = basePosition + exefsOffset;

            byte[] exefsBuf = new byte[exefsHashSize];

            await output.ReadExactlyAsync(exefsBuf, ct);

            SHA256.HashData(exefsBuf).CopyTo(ncchHdr, 0x1C0);
        }

        if (romfsSize > 0)
        {
            output.Position = basePosition + romfsOffset;

            byte[] romfsBuf = new byte[romfsHashSize];

            await output.ReadExactlyAsync(romfsBuf, ct);

            SHA256.HashData(romfsBuf).CopyTo(ncchHdr, 0x1E0);
        }

        output.Position = basePosition;
        await output.WriteAsync(ncchHdr, ct);
    }

    public static async Task<long> CalculateSizeAsync(NcchUnpackResult unpack, byte[] exefsBlock, RomFsUnpackResult? romFs, IRomFsFileSource? patchSource, Stream ncchSource, CancellationToken ct = default)
    {
        long ncchSize = MediaUnit;

        if (unpack.ExHeader != null)
            ncchSize += unpack.ExHeader.Length;

        if (unpack.Logo != null)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(unpack.Logo.Length, MediaUnit);

        if (unpack.PlainRegion != null)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(unpack.PlainRegion.Length, MediaUnit);

        if (exefsBlock.Length > 0)
            ncchSize = AlignUp(ncchSize, MediaUnit) + AlignUp(exefsBlock.Length, MediaUnit);

        if (romFs != null)
        {
            Dictionary<string, long>? patchSizeMap = null;

            if (patchSource != null)
            {
                long dataBase = romFs.DataLevel2Offset + romFs.RomFsHeader.DataOffset;
                patchSizeMap = await RomFsPacker.BuildPatchSizeMapAsync(romFs.Files, patchSource, ncchSource, dataBase, ct);
            }

            var (totalSize, _, _, _, _) = RomFsPacker.CalculateLayout(romFs.Directories, romFs.Files, patchSizeMap);
            ncchSize = AlignUp(ncchSize, RomFsAlign) + AlignUp((long)totalSize, MediaUnit);
        }

        return AlignUp(ncchSize, MediaUnit);
    }

    private static long AlignUp(long v, long a) => (v + a - 1) & ~(a - 1);

    private static long AlignUp(int v, int a) => AlignUp((long)v, (long)a);
}