using Common;
using N3DS.Core.Crypto;
using N3DS.Core.Interfaces;
using N3DS.Core.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace N3DS.Core.Services;

public static class CiaBuilder
{
    private const int CiaAlign = 64;
    private const int MediaUnit = 0x200;
    private const uint SigTypeRsa2048Sha256 = 0x00010004;
    private const int TicketSize = 0x140 + 0x164 + 0x28 + 0x84;
    private const int TmdBaseSize = 0x140 + 0xC4 + (0x24 * 64);
    private const int TmdChunkSize = 0x30;
    private const int HashStreamBufferSize = 1024 * 1024;

    public static async Task BuildAsync(INcsdSource ctx, KeyStore keyStore, Stream output, byte[]? exHeaderPart0, byte[]? smdhDataPart0, Action<long, long>? progress = null, IProgress<ProgressInfo>? hashProgress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        if (!output.CanSeek || !output.CanRead)
            throw new NotSupportedException("CIA를 직접 생성하려면 읽기/탐색이 가능한 출력 스트림이 필요합니다(FileStream 등).");

        var partitions = new List<(int index, NcchHeader header, long size)>();

        foreach (var chunk in ctx.Contents.Where(c => c.ContentIndex <= 7).OrderBy(c => c.ContentIndex))
        {
            var header = await ctx.GetNcchHeaderAsync(chunk.ContentIndex, ct);

            partitions.Add((chunk.ContentIndex, header, chunk.ContentSize));
        }

        if (partitions.Count == 0)
            throw new InvalidOperationException("유효한 NCCH 파티션을 찾을 수 없습니다.");

        var part0 = partitions.First(p => p.index == 0);
        ulong titleId = part0.header.ProgramId;
        uint titleType = (uint)(titleId >> 32);

        if (titleType == 0x0004000E)
            throw new NotSupportedException("이 파일은 [업데이트 패치]입니다. 업데이트 파일은 CIA로 만들 수 없습니다.");
        else if (titleType == 0x0004008C)
            throw new NotSupportedException("이 파일은 [DLC] 콘텐츠입니다. DLC 파일은 CIA 변환을 지원하지 않습니다.");
        else if (titleType != 0x00040000)
            throw new NotSupportedException($"지원하지 않는 소프트웨어 타입입니다. (Title ID Type: 0x{titleType:X8})");

        uint saveSize = exHeaderPart0 is { Length: > 0x1C3 } ? BinaryPrimitives.ReadUInt32LittleEndian(exHeaderPart0.AsSpan(0x1C0)) : 0;
        int contentCount = partitions.Count;
        byte[] titleKey = RandomNumberGenerator.GetBytes(16);
        string certsPath = Path.Combine(AppContext.BaseDirectory, "certs.bin");

        if (!File.Exists(certsPath))
            throw new CertsBinNotFoundException("certs.bin 추출 필요 / 유틸 - certs.bin 추출을 진행하세요");

        byte[] certChain = await File.ReadAllBytesAsync(certsPath, ct);
        uint certChainSize = (uint)certChain.Length;
        uint ticketSize = TicketSize;
        uint tmdSize = (uint)(TmdBaseSize + TmdChunkSize * contentCount);
        ulong ciaContentSize = (ulong)partitions.Sum(p => AlignUp(p.size, CiaAlign));

        WriteCiaHeader(output, partitions, certChainSize, ticketSize, tmdSize, ciaContentSize);

        long certOffset = AlignUp(0x2020, CiaAlign);

        output.Position = certOffset;

        await output.WriteAsync(certChain, ct);

        long ticketOffset = AlignUp(certOffset + certChainSize, CiaAlign);

        output.Position = ticketOffset;

        await output.WriteAsync(BuildTicket(keyStore, titleId, titleKey, partitions), ct);

        long tmdOffset = AlignUp(ticketOffset + ticketSize, CiaAlign);
        var zeroHashes = new byte[contentCount][];

        for (int i = 0; i < contentCount; i++)
            zeroHashes[i] = new byte[0x20];

        output.Position = tmdOffset;

        await output.WriteAsync(BuildTmd(titleId, partitions, zeroHashes, saveSize, part0.header.Version), ct);

        long firstContentOffset = AlignUp(tmdOffset + tmdSize, CiaAlign);
        var contentOffsets = new long[contentCount];
        long totalBytes = partitions.Sum(p => p.size);
        long processedBytes = 0;

        output.Position = firstContentOffset;

        for (int i = 0; i < contentCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            contentOffsets[i] = output.Position;

            var (index, _, size) = partitions[i];
            long partitionStartProcessed = processedBytes;

            await ctx.WriteContentAsync(index, output, size, (written, _) => { progress?.Invoke(partitionStartProcessed + written, totalBytes); }, ct);

            processedBytes = partitionStartProcessed + size;

            long aligned = AlignUp(size, CiaAlign);
            long padding = aligned - size;

            if (padding > 0)
            {
                output.Position = contentOffsets[i] + size;
                await output.WriteAsync(new byte[padding], ct);
            }
        }

        progress?.Invoke(totalBytes, totalBytes);

        var contentHashes = new byte[contentCount][];
        var hashReporter = hashProgress == null ? null : new ProgressReporter("해시 계산 중...", string.Empty, totalBytes, hashProgress);
        var hashAction = hashReporter?.CreateAction();
        long hashedBytes = 0;

        hashReporter?.ForceReport();

        for (int i = 0; i < contentCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (_, _, size) = partitions[i];
            long hashedBase = hashedBytes;

            output.Position = contentOffsets[i];

            contentHashes[i] = await HashRangeAsync(output, size, read => { hashAction?.Invoke(hashedBase + read, totalBytes); }, ct);

            hashedBytes = hashedBase + size;
        }

        hashAction?.Invoke(totalBytes, totalBytes);

        output.Position = tmdOffset;

        await output.WriteAsync(BuildTmd(titleId, partitions, contentHashes, saveSize, part0.header.Version), ct);

        long metaOffset = firstContentOffset + (long)ciaContentSize;

        output.Position = metaOffset;

        output.SetLength(metaOffset + 0x3AC0);
        await output.WriteAsync(BuildMeta(smdhDataPart0, exHeaderPart0), ct);

        log?.Invoke("CIA 직접 생성 완료 (중간 CCI 파일 없음)", LogLevel.Ok);
    }

    private static async Task<byte[]> HashRangeAsync(Stream stream, long size, Action<long>? onProgress, CancellationToken ct)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var pool = ArrayPool<byte>.Shared;
        byte[] buf = pool.Rent(HashStreamBufferSize);

        try
        {
            long remaining = size;
            long readSoFar = 0;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                int toRead = (int)Math.Min(buf.Length, remaining);
                int read = await stream.ReadAsync(buf.AsMemory(0, toRead), ct);

                if (read == 0)
                    break;

                sha.AppendData(buf, 0, read);
                remaining -= read;
                readSoFar += read;
                onProgress?.Invoke(readSoFar);
            }
        }
        finally 
        {
            pool.Return(buf);
        }

        return sha.GetHashAndReset();
    }

    private static void WriteCiaHeader(Stream output, List<(int index, NcchHeader header, long size)> partitions, uint certChainSize, uint ticketSize, uint tmdSize, ulong contentSize)
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

        foreach (var (index, _, _) in partitions)
            buf[0x20 + index / 8] |= (byte)(0x80 >> (index & 7));

        output.Position = 0;
        output.Write(buf);
    }

    private static byte[] BuildTicket(KeyStore keyStore, ulong titleId, byte[] titleKey, List<(int index, NcchHeader header, long size)> partitions)
    {
        byte[] buf = new byte[TicketSize];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigTypeRsa2048Sha256);

        int h = 0x140;

        "Root-CA00000004-XS00000009"u8.CopyTo(buf.AsSpan(h));

        buf[h + 0x7C] = 0x01;
        buf[h + 0x7D] = 0x00;
        buf[h + 0x7E] = 0x00;

        byte[] encTitleKey = EncryptTitleKey(keyStore, titleKey, titleId);

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

    private static byte[] EncryptTitleKey(KeyStore keyStore, byte[] titleKey, ulong titleId)
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

    private static byte[] BuildTmd(ulong titleId, List<(int index, NcchHeader header, long size)> partitions, byte[][] contentHashes, uint saveSize, ushort titleVersion)
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
            var (index, _, size) = partitions[i];
            int off = chunkOff + i * TmdChunkSize;

            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off + 0x00), (uint)index);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x04), (ushort)index);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x06), 0x0000);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(off + 0x08), (ulong)size);
            contentHashes[i].CopyTo(buf, off + 0x10);
        }

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff + 0x00), 0);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff + 0x02), (ushort)contentCount);
        SHA256.HashData(buf.AsSpan(chunkOff, TmdChunkSize * contentCount)).CopyTo(buf, infoOff + 0x04);
        SHA256.HashData(buf.AsSpan(infoOff, 0x24 * 64)).CopyTo(buf, hdr + 0xA4);

        return buf;
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

    private static long AlignUp(long value, long alignment) => (value + alignment - 1) & ~(alignment - 1);
}