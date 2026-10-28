using Common;
using LibHac.Crypto;
using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace NSW.HacPack.Services;

public static class NcaCryptoService
{
    public static void EncryptSection(Stream ncaFile, ref NcaHeader hdr, int sectionIndex, NcaGenerationOptions settings, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ulong startOffset = hdr.SectionEntries[sectionIndex].MediaStartOffset * 0x200UL;
        ulong endOffset = hdr.SectionEntries[sectionIndex].MediaEndOffset * 0x200UL;
        ulong fileSize = endOffset - startOffset;
        byte[] encKey = new byte[0x10];

        if (settings.HasTitleKey == 1)
            Array.Copy(settings.TitleKey, encKey, 0x10);
        else
            Array.Copy(hdr.EncryptedKeys, 2 * 0x10, encKey, 0, 0x10);

        ulong ctrOfs = startOffset >> 4;
        byte[] baseCtr = new byte[0x10];

        for (int j = 0; j < 8; j++)
        {
            baseCtr[j] = hdr.FsHeaders[sectionIndex].SectionCtr[8 - j - 1];
            baseCtr[0x10 - j - 1] = (byte)(ctrOfs & 0xFF);
            ctrOfs >>= 8;
        }

        const int batchSize = 0x10000000;
        const int chunkSize = 0x1000000;

        byte[] batch = new byte[batchSize];
        var reporter = new ProgressReporter("암호화 중", string.Empty, (long)fileSize, progress);
        using var mmf = ncaFile is FileStream mappedFile ? MemoryMappedFile.CreateFromFile(mappedFile, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true) : null;
        ulong ofs = 0;

        while (ofs < fileSize)
        {
            ct.ThrowIfCancellationRequested();

            int batchLen = (int)Math.Min(batchSize, fileSize - ofs);
            if (mmf != null)
            {
                using var view = mmf.CreateViewStream((long)(startOffset + ofs), batchLen, MemoryMappedFileAccess.Read);

                view.ReadExactly(batch, 0, batchLen);
            }
            else
            {
                ncaFile.Position = (long)(startOffset + ofs);

                ncaFile.ReadExactly(batch, 0, batchLen);
            }
            int chunkCount = (batchLen + chunkSize - 1) / chunkSize;

            Parallel.For(0, chunkCount, new ParallelOptions { CancellationToken = ct }, i =>
            {
                int chunkOffset = i * chunkSize;
                int chunkLen = Math.Min(chunkSize, batchLen - chunkOffset);
                ulong absOffset = startOffset + ofs + (ulong)chunkOffset;
                Span<byte> ctr = stackalloc byte[0x10];

                baseCtr.AsSpan(0, 8).CopyTo(ctr);
                UpdateCtr(ctr, absOffset);

                using var ctx = new AesContext(encKey, AesMode.CTR);

                ctx.SetIV(ctr.ToArray());
                ctx.Encrypt(batch.AsSpan(chunkOffset, chunkLen), batch.AsSpan(chunkOffset, chunkLen));
            });

            if (mmf != null)
            {
                using var view = mmf.CreateViewStream((long)(startOffset + ofs), batchLen, MemoryMappedFileAccess.ReadWrite);

                view.Write(batch, 0, batchLen);
            }
            else
            {
                ncaFile.Position = (long)(startOffset + ofs);

                ncaFile.Write(batch, 0, batchLen);
            }

            ofs += (ulong)batchLen;

            reporter.AddProgress(batchLen);
        }

        reporter.ForceReport();
    }

    public static void EncryptKeyArea(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        int keygenIdx = settings.KeyGeneration - 1;
        var kaek = settings.KeySet.KeyAreaKeys[keygenIdx][0];
        byte[] keys = new byte[0x40];

        Array.Copy(hdr.EncryptedKeys, keys, 0x40);
        Aes.EncryptEcb128(keys.AsSpan(), keys.AsSpan(), kaek);
        Array.Copy(keys, hdr.EncryptedKeys, 0x40);
    }

    public static void EncryptHeader(ref NcaHeader hdr, NcaGenerationOptions settings)
    {
        byte[] rawHdr = NcaStructHelper.StructToBytes(ref hdr);
        byte[] key1 = settings.KeySet.HeaderKey.DataKey.DataRo.ToArray();
        byte[] key2 = settings.KeySet.HeaderKey.TweakKey.DataRo.ToArray();

        for (ulong sector = 0; sector < 6; sector++)
        {
            int offset = (int)(sector * 0x200);
            byte[] block = new byte[0x200];

            Array.Copy(rawHdr, offset, block, 0, 0x200);
            XtsCrypto.Encrypt(block, key1, key2, sector);
            Array.Copy(block, 0, rawHdr, offset, 0x200);
        }

        NcaStructHelper.BytesToStruct(rawHdr, ref hdr);
    }

    public static void UpdateCtr(Span<byte> ctr, ulong ofs)
    {
        ofs >>= 4;

        for (int j = 0; j < 8; j++)
        {
            ctr[0x10 - j - 1] = (byte)(ofs & 0xFF);
            ofs >>= 8;
        }
    }
}