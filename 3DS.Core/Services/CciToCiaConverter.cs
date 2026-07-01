using _3DS.Core.Crypto;
using _3DS.Core.IO;
using _3DS.Core.Models;
using Common;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public class CciToCiaConverter(KeyStore keyStore)
{
    private const int CiaAlign = 64;
    private const int MediaUnit = 0x200;
    private const uint SigTypeRsa2048Sha256 = 0x00010004;
    private const int TicketSize = 0x140 + 0x164 + 0x28 + 0x84;
    private const int TmdBaseSize = 0x140 + 0xC4 + (0x24 * 64);
    private const int TmdChunkSize = 0x30;

    public async Task ConvertAsync(string inputPath, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel, string>? log = null, CancellationToken ct = default)
    {
        string? outputPath = null;
        bool isCompleted = false;

        try
        {
            outputPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, ".cia"));
            log?.Invoke($"{Path.GetFileName(inputPath)} → CIA 변환 시작", LogLevel.Highlight, string.Empty);

            using var inputStream = File.Open(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var outputStream = File.Open(outputPath, FileMode.Create, FileAccess.Write);

            await ConvertAsync(inputStream, outputStream, progress, log, ct);

            isCompleted = true;
            log?.Invoke($"변환 완료: {outputPath}", LogLevel.Ok, string.Empty);
        }
        finally
        {
            if (!isCompleted && !string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    private async Task ConvertAsync(Stream input, Stream output, IProgress<ProgressInfo>? progress = null, Action<string, LogLevel, string>? log = null, CancellationToken ct = default)
    {
        uint saveSize = 0;
        byte[]? exheader = null;
        var ncsdHeader = await ParseNcsdHeaderAsync(input, ct);
        var partitions = new List<(int index, long offset, long size, NcchHeader ncch, long actualSize)>();

        for (int i = 0; i < 8; i++)
        {
            var (partOffset, partSize) = ncsdHeader.PartitionMap[i];

            if (partOffset == 0 || partSize == 0)
                continue;

            long byteOffset = (long)partOffset * MediaUnit;
            long byteSize = (long)partSize * MediaUnit;
            byte[] ncchBuf = new byte[0x200];

            input.Position = byteOffset;
            await input.ReadExactlyAsync(ncchBuf, ct);

            var ncch = NcchHeader.Parse(ncchBuf, 0);

            if (i == 0 && ncch.ExtendedHeaderSize > 0)
            {
                Stream exhdrStream = new SubStream(input, byteOffset, byteSize);

                if (!ncch.NoCrypto)
                {
                    log?.Invoke("암호화된 롬 감지, 복호화 파이프라인 구동...", LogLevel.Info, string.Empty);
                    exhdrStream = new NcchDecryptionStream(exhdrStream, 0, keyStore);
                }

                await using (exhdrStream)
                {
                    exheader = new byte[0x400];
                    exhdrStream.Position = 0x200;
                    await exhdrStream.ReadExactlyAsync(exheader, ct);
                    saveSize = BinaryPrimitives.ReadUInt32LittleEndian(exheader.AsSpan(0x1C0));
                }
            }

            partitions.Add((i, byteOffset, byteSize, ncch, (long)ncch.ContentSize * MediaUnit));
        }

        if (partitions.Count == 0)
            throw new InvalidDataException("유효한 NCCH 파티션을 찾을 수 없습니다.");

        ulong titleId = partitions.First(p => p.index == 0).ncch.ProgramId;
        uint titleType = (uint)(titleId >> 32);

        if (titleType == 0x0004000E)
            throw new NotSupportedException("이 파일은 [업데이트 패치]입니다. 업데이트 파일은 CCI로 변환하거나 본편과 합칠 수 없습니다.");
        else if (titleType == 0x0004008C)
            throw new NotSupportedException("이 파일은 [DLC] 콘텐츠입니다. DLC 파일은 CCI 변환을 지원하지 않습니다.");
        else if (titleType != 0x00040000)
            throw new NotSupportedException($"지원하지 않는 소프트웨어 타입입니다. (Title ID Type: 0x{titleType:X8})");

        byte[]? smdhData = null;
        var part0 = partitions.First(p => p.index == 0);
        Stream iconStream = new SubStream(input, part0.offset, part0.actualSize);

        if (!part0.ncch.NoCrypto)
            iconStream = new NcchDecryptionStream(iconStream, 0, keyStore);

        await using (iconStream)
            smdhData = await ReadExeFsIconAsync(iconStream, part0.ncch, ct);

        int contentCount = partitions.Count;
        byte[] titleKey = RandomNumberGenerator.GetBytes(16);
        var contentHashes = new byte[contentCount][];
        long totalBytesToProcess = partitions.Sum(p => p.actualSize) * 2;
        long totalProcessedBytes = 0;

        for (int i = 0; i < contentCount; i++)
        {
            var (_, partOffset, _, ncch, actualSize) = partitions[i];
            Stream ncchStream = new SubStream(input, partOffset, actualSize);

            if (!ncch.NoCrypto)
                ncchStream = new NcchDecryptionStream(ncchStream, 0, keyStore);

            await using (ncchStream)
            {
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var pool = ArrayPool<byte>.Shared;
                byte[] buf = pool.Rent(1024 * 1024);

                try
                {
                    long remaining = actualSize;

                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buf.Length, remaining);
                        int read = await ncchStream.ReadAsync(buf.AsMemory(0, toRead), ct);

                        if (read == 0)
                            break;

                        sha.AppendData(buf, 0, read);
                        remaining -= read;
                        totalProcessedBytes += read;
                        progress?.Report(new ProgressInfo
                        {
                            Percent = (int)((double)totalProcessedBytes / totalBytesToProcess * 100)
                        });
                    }
                }
                finally { pool.Return(buf); }

                contentHashes[i] = sha.GetCurrentHash();
            }
        }

        string certsPath = Path.Combine(AppContext.BaseDirectory, "certs.bin");

        if (!File.Exists(certsPath))
            throw new CertsBinNotFoundException("certs.bin 추출 필요 / 유틸 - certs.bin 추출을 진행하세요");

        byte[] certChain = await File.ReadAllBytesAsync(certsPath, ct);

        uint certChainSize = (uint)certChain.Length;
        uint ticketSize = TicketSize;
        uint tmdSize = (uint)(TmdBaseSize + TmdChunkSize * contentCount);
        ulong contentSize = (ulong)partitions.Sum(p => AlignUp(p.actualSize, CiaAlign));

        WriteCiaHeader(output, partitions, certChainSize, ticketSize, tmdSize, contentSize);

        long certOffset = AlignUp(0x2020, CiaAlign);

        output.Position = certOffset;
        await output.WriteAsync(certChain, ct);

        long ticketOffset = AlignUp(certOffset + certChainSize, CiaAlign);

        output.Position = ticketOffset;
        await output.WriteAsync(BuildTicket(titleId, titleKey, partitions), ct);

        long tmdOffset = AlignUp(ticketOffset + ticketSize, CiaAlign);

        output.Position = tmdOffset;

        var mainNcch = partitions.First(p => p.index == 0).ncch;

        await output.WriteAsync(BuildTmd(titleId, partitions, contentHashes, saveSize, mainNcch.Version), ct);

        long firstContentOffset = AlignUp(tmdOffset + tmdSize, CiaAlign);

        output.Position = firstContentOffset;

        long totalBytes = partitions.Sum(p => p.actualSize);

        foreach (var (_, partOffset, _, ncch, actualSize) in partitions)
        {
            ct.ThrowIfCancellationRequested();

            Stream ncchStream = new SubStream(input, partOffset, actualSize);

            if (!ncch.NoCrypto)
                ncchStream = new NcchDecryptionStream(ncchStream, 0, keyStore);

            await using (ncchStream)
            {
                await CopyWithProgressAsync(ncchStream, output, actualSize, bytesWritten =>
                {
                    totalProcessedBytes += bytesWritten;
                    progress?.Report(new ProgressInfo
                    {
                        Percent = (int)((double)totalProcessedBytes / totalBytesToProcess * 100)
                    });
                }, ct);
            }

            long aligned = AlignUp(actualSize, CiaAlign);
            long padding = aligned - actualSize;

            if (padding > 0)
                await output.WriteAsync(new byte[padding], ct);
        }

        byte[] meta = BuildMeta(smdhData, exheader);
        await output.WriteAsync(meta, ct);

        progress?.Report(new ProgressInfo { Percent = 100 });
    }

    private static void WriteCiaHeader(Stream output, List<(int index, long offset, long size, NcchHeader ncch, long actualSize)> partitions, uint certChainSize, uint ticketSize, uint tmdSize, ulong contentSize)
    {
        byte[] buf = new byte[0x2020];

        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x00), 0x2020);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x04), 0x0000);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x06), 0x0000);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x08), certChainSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x0C), ticketSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x10), tmdSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x14), 0x3AC0);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18), contentSize);

        foreach (var (index, _, _, _, _) in partitions)
            buf[0x20 + index / 8] |= (byte)(0x80 >> (index & 7));

        output.Write(buf);
    }

    private byte[] BuildTicket(ulong titleId, byte[] titleKey, List<(int index, long offset, long size, NcchHeader ncch, long actualSize)> partitions)
    {
        byte[] buf = new byte[TicketSize];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigTypeRsa2048Sha256);

        int h = 0x140;

        "Root-CA00000004-XS00000009"u8.CopyTo(buf.AsSpan(h));

        buf[h + 0x7C] = 0x01;
        buf[h + 0x7D] = 0x00;
        buf[h + 0x7E] = 0x00;

        byte[] encTitleKey = EncryptTitleKey(titleKey, titleId);

        encTitleKey.CopyTo(buf, h + 0x7F);

        ulong ticketId = 0x0004000000000000UL | ((ulong)Random.Shared.NextInt64() & 0x0000FFFFFFFFFFFFUL);

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(h + 0x90), ticketId);
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(h + 0x9C), titleId);

        buf[h + 0xB0] = 0x00;
        buf[h + 0xB1] = 0x00;

        int idxHdr = h + 0x164;
        int segNum = 1;
        int segSize = 0x84;
        int segTotalSize = segSize * segNum;
        int totalIdxSize = 0x28 + segTotalSize;

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x00), 0x00010014);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x04), (uint)totalIdxSize);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x08), 0x00000014);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x0C), 0x00010014);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x10), 0x00000000);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x14), 0x00000028);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x18), (uint)segNum);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x1C), (uint)segSize);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x20), (uint)segTotalSize);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxHdr + 0x24), 0x00030000);

        int idxData = idxHdr + 0x28;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(idxData + 0x00), 0x00000000);

        int contentCount = partitions.Count;

        for (int i = 0; i < contentCount; i++)
        {
            int index = partitions[i].index;

            buf[idxData + 0x04 + (index & 0x3FF) / 8] |= (byte)(1 << (index & 0x7));
        }

        return buf;
    }

    private byte[] EncryptTitleKey(byte[] titleKey, ulong titleId)
    {
        byte[] commonKey = keyStore.GetCommonKey(0);
        byte[] iv = new byte[16];

        BinaryPrimitives.WriteUInt64BigEndian(iv.AsSpan(0), titleId);

        using var aes = Aes.Create();

        aes.Key = commonKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;

        using var enc = aes.CreateEncryptor();

        return enc.TransformFinalBlock(titleKey, 0, 16);
    }

    private static byte[] BuildTmd(ulong titleId, List<(int index, long offset, long size, NcchHeader ncch, long actualSize)> partitions, byte[][] contentHashes, uint saveSize, ushort titleVersion)
    {
        int contentCount = partitions.Count;
        int totalSize = TmdBaseSize + TmdChunkSize * contentCount;
        byte[] buf = new byte[totalSize];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigTypeRsa2048Sha256);

        int hdr = 0x140;

        "Root-CA00000004-CP0000000a"u8.CopyTo(buf.AsSpan(hdr));

        buf[hdr + 0x40] = 0x01;
        buf[hdr + 0x41] = 0x00;
        buf[hdr + 0x42] = 0x00;

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(hdr + 0x4C), titleId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(hdr + 0x54), 0x00000040);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(hdr + 0x5A), saveSize);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(hdr + 0x9C), titleVersion);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(hdr + 0x9E), (ushort)contentCount);

        int infoOff = hdr + 0xC4;
        int chunkOff = infoOff + 0x24 * 64;

        for (int i = 0; i < contentCount; i++)
        {
            var (index, _, _, _, actualSize) = partitions[i];
            int off = chunkOff + i * TmdChunkSize;

            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off + 0x00), (uint)index);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x04), (ushort)index);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x06), 0x0000);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(off + 0x08), (ulong)actualSize);
            contentHashes[i].CopyTo(buf, off + 0x10);
        }

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff + 0x00), 0);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff + 0x02), (ushort)contentCount);
        SHA256.HashData(buf.AsSpan(chunkOff, TmdChunkSize * contentCount)).CopyTo(buf, infoOff + 0x04);
        SHA256.HashData(buf.AsSpan(infoOff, 0x24 * 64)).CopyTo(buf, hdr + 0xA4);

        return buf;
    }

    private static async Task<NcsdHeader> ParseNcsdHeaderAsync(Stream input, CancellationToken ct)
    {
        byte[] buf = new byte[0x200];

        input.Position = 0;
        await input.ReadExactlyAsync(buf, ct);

        if (!buf.AsSpan(0x100, 4).SequenceEqual("NCSD"u8))
            throw new InvalidDataException("NCSD 매직 번호 불일치");

        ulong mediaId = BinaryPrimitives.ReadUInt64LittleEndian(buf.AsSpan(0x108));
        var partitionMap = new (uint offset, uint size)[8];

        for (int i = 0; i < 8; i++)
        {
            int off = 0x120 + i * 8;
            partitionMap[i] = (BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(off)), BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(off + 4)));
        }

        return new NcsdHeader { MediaId = mediaId, PartitionMap = partitionMap };
    }

    private static async Task CopyWithProgressAsync(Stream input, Stream output, long size, Action<long> onProgress, CancellationToken ct)
    {
        var pool = ArrayPool<byte>.Shared;
        byte[] buf = pool.Rent(1024 * 1024);

        try
        {
            long remaining = size;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                int toRead = (int)Math.Min(buf.Length, remaining);
                int read = await input.ReadAsync(buf.AsMemory(0, toRead), ct);

                if (read == 0)
                    break;

                await output.WriteAsync(buf.AsMemory(0, read), ct);
                onProgress(read);
                remaining -= read;
            }
        }
        finally { pool.Return(buf); }
    }

    private static byte[] BuildMeta(byte[]? smdhData, byte[]? exheader)
    {
        byte[] buf = new byte[0x3AC0];

        if (exheader != null)
        {
            exheader.AsSpan(0x40, 0x180).CopyTo(buf.AsSpan(0x000));
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x300), BinaryPrimitives.ReadUInt32LittleEndian(exheader.AsSpan(0x320)));
        }

        smdhData?.CopyTo(buf, 0x400);

        return buf;
    }

    private static async Task<byte[]?> ReadExeFsIconAsync(Stream ncchStream, NcchHeader ncch, CancellationToken ct)
    {
        if (ncch.ExefsOffset == 0) return null;

        long exefsStart = (long)ncch.ExefsOffset * 0x200;
        byte[] exefsHeader = new byte[0x200];

        ncchStream.Position = exefsStart;
        await ncchStream.ReadExactlyAsync(exefsHeader, ct);

        for (int i = 0; i < 8; i++)
        {
            int entryOff = i * 16;
            string name = System.Text.Encoding.ASCII.GetString(exefsHeader, entryOff, 8).TrimEnd('\0');
            uint sectionOffset = BinaryPrimitives.ReadUInt32LittleEndian(exefsHeader.AsSpan(entryOff + 8));
            uint sectionSize = BinaryPrimitives.ReadUInt32LittleEndian(exefsHeader.AsSpan(entryOff + 12));

            if (name == "icon" && sectionSize > 0)
            {
                byte[] iconData = new byte[sectionSize];
                ncchStream.Position = exefsStart + 0x200 + sectionOffset;
                await ncchStream.ReadExactlyAsync(iconData, ct);
                return iconData;
            }
        }

        return null;
    }

    private static long AlignUp(long value, long alignment) => (value + alignment - 1) & ~(alignment - 1);
    private static long AlignUp(uint value, long alignment) => AlignUp((long)value, alignment);
}