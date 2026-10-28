using NSW.HacPack.Models;
using System.IO;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class NcaHeaderService
{
    public static void InitHeader(ref NcaHeader hdr)
    {
        hdr.FixedKeySig = new byte[0x100];
        hdr.NpdmKeySig = new byte[0x100];
        hdr._0x218 = new byte[0x4];
        hdr._0x221 = new byte[0xF];
        hdr.RightsId = new byte[0x10];
        hdr.SectionEntries = new NcaSectionEntry[4];
        hdr.SectionHashes = new byte[4 * 0x20];
        hdr.EncryptedKeys = new byte[4 * 0x10];
        hdr._0x340 = new byte[0xC0];
        hdr.FsHeaders = new NcaFsHeader[4];

        for (int i = 0; i < 4; i++)
        {
            hdr.FsHeaders[i].SuperblockRaw = new byte[0x138];
            hdr.FsHeaders[i].SectionCtr = new byte[0x8];

            Random.Shared.NextBytes(hdr.FsHeaders[i].SectionCtr);

            hdr.FsHeaders[i]._0x148 = new byte[0xB8];
            hdr.FsHeaders[i]._0x5 = new byte[0x3];
            hdr.SectionEntries[i]._0x8 = new byte[0x8];
        }
    }

    public static void SetCommonHeader(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        hdr.Magic = 0x3341434E;
        hdr.SdkVersion = settings.SdkVersion;
        hdr.TitleId = settings.TitleId + settings.IdOffset;

        if (settings.NcaDistType == LibHac.FsSystem.NcaHeader.DistributionType.GameCard)
            hdr.Distribution = 1;

        SetKeygen(ref hdr, settings);
    }

    public static void SetKeygen(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        if (settings.KeyGeneration == 1)
            return;

        hdr.CryptoType = 0x2;

        if (settings.KeyGeneration > 2)
            hdr.CryptoType2 = (byte)settings.KeyGeneration;
    }

    public static void SetSectionEntry(ref NcaHeader hdr, int idx, uint start, uint end)
    {
        hdr.SectionEntries[idx].MediaStartOffset = start;
        hdr.SectionEntries[idx].MediaEndOffset = end;
        hdr.SectionEntries[idx]._0x8[0] = 0x1;
    }

    public static void SetFsHeader(ref NcaHeader hdr, int idx, byte hashType, byte fsType, byte version, byte cryptType)
    {
        hdr.FsHeaders[idx].HashType = hashType;
        hdr.FsHeaders[idx].FsType = fsType;
        hdr.FsHeaders[idx].Version = version;
        hdr.FsHeaders[idx].CryptType = cryptType;
    }

    public static void SetIvfcMagic(ref NcaHeader hdr, int idx)
    {
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x00, IvfcConstants.MagicIvfc);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x04, 0x20000);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x08, 0x20);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x0C, 0x7);
    }

    public static void SetIvfcMasterHash(ref NcaHeader hdr, int idx, byte[] hash) => Array.Copy(hash, 0, hdr.FsHeaders[idx].SuperblockRaw, 0xC0, 0x20);

    public static void SetIvfcLevel5(ref NcaHeader hdr, int idx, ulong size)
    {
        int ofs = 0x10 + 5 * 0x18;

        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, ofs, 0);
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, ofs + 0x08, size);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, ofs + 0x10, 0x0E);
    }

    public static void SetIvfcLevel(ref NcaHeader hdr, int idx, int level, ulong size)
    {
        int ofs = 0x10 + level * 0x18;

        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, ofs + 0x08, size);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, ofs + 0x10, 0x0E);
    }

    public static void SetIvfcLogicalOffsets(ref NcaHeader hdr, int idx)
    {
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x10, 0);

        for (int i = 1; i <= 5; i++)
        {
            int prevOfs = 0x10 + (i - 1) * 0x18;
            ulong prev = ReadU64(hdr.FsHeaders[idx].SuperblockRaw, prevOfs);
            ulong prevSize = ReadU64(hdr.FsHeaders[idx].SuperblockRaw, prevOfs + 0x08);

            WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x10 + i * 0x18, prev + prevSize);
        }
    }

    public static void SetIvfcLogicalOffsetsDataFirst(ref NcaHeader hdr, int idx, ulong hashLevelsStart)
    {
        byte[] raw = hdr.FsHeaders[idx].SuperblockRaw;
        ulong offset = hashLevelsStart;

        for (int i = 0; i < 5; i++)
        {
            int ofs = 0x10 + i * 0x18;

            WriteU64(raw, ofs, offset);

            offset += ReadU64(raw, ofs + 0x08);
        }

        WriteU64(raw, 0x10 + 5 * 0x18, 0);
    }

    public static void SetPfs0Superblock(ref NcaHeader hdr, int idx, ulong pfs0Size, ulong hashTableSize, ulong pfs0Offset, uint blockSize)
    {
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x20, blockSize);
        WriteU32(hdr.FsHeaders[idx].SuperblockRaw, 0x24, 0x2);
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x28, 0);
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x30, hashTableSize);
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x38, pfs0Offset);
        WriteU64(hdr.FsHeaders[idx].SuperblockRaw, 0x40, pfs0Size);
    }

    public static void SetCompressionInfo(ref NcaHeader hdr, int idx, long tableOffset, long tableSize, byte[] bucketTreeHeader)
    {
        byte[] area = hdr.FsHeaders[idx]._0x148;

        WriteU64(area, 0x30, (ulong)tableOffset);
        WriteU64(area, 0x38, (ulong)tableSize);
        Array.Copy(bucketTreeHeader, 0, area, 0x40, 0x10);
    }

    public static void SetPfs0MasterHash(ref NcaHeader hdr, int idx, byte[] hash) => Array.Copy(hash, 0, hdr.FsHeaders[idx].SuperblockRaw, 0, 0x20);

    public static void SetSectionHash(ref NcaHeader hdr, int idx, byte[] hash) => Array.Copy(hash, 0, hdr.SectionHashes, idx * 0x20, 0x20);

    public static void FinalizeKeyArea(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        if (settings.HasTitleKey == 0)
            Array.Copy(settings.KeyAreaKey!, 0, hdr.EncryptedKeys, 2 * 0x10, 0x10);
        else
        {
            for (int i = 0; i < 8; i++)
                hdr.RightsId[7 - i] = (byte)(settings.TitleId >> 8 * i & 0xFF);

            hdr.RightsId[15] = (byte)settings.KeyGeneration;
        }
    }

    public static void FinalizeNca(Stream ncaFile, ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        ncaFile.Seek(0, SeekOrigin.End);
        hdr.NcaSize = (ulong)ncaFile.Position;

        if (settings.HasTitleKey == 0)
            NcaCryptoService.EncryptKeyArea(ref hdr, settings);
        else
        {
            TicketService.GenerateCertificate(settings);
            TicketService.GenerateTicket(settings);
        }

        NcaSigningService.SignNca(ref hdr, settings);
        NcaCryptoService.EncryptHeader(ref hdr, settings);

        ncaFile.Seek(0, SeekOrigin.Begin);
        NcaStructHelper.WriteStruct(ncaFile, hdr);
    }

    private static void WriteU32(byte[] buf, int ofs, uint v) => MemoryMarshal.Write(buf.AsSpan(ofs), in v);

    private static void WriteU64(byte[] buf, int ofs, ulong v) => MemoryMarshal.Write(buf.AsSpan(ofs), in v);

    private static ulong ReadU64(byte[] buf, int ofs) => MemoryMarshal.Read<ulong>(buf.AsSpan(ofs));
}