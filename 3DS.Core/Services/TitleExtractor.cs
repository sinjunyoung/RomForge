using _3DS.Core.Crypto;
using _3DS.Core.FileSystem;
using _3DS.Core.IO;
using _3DS.Core.Models;
using Common;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public class TitleExtractor(KeyStore keyStore, SdCrypto sdCrypto, SdTitleScanner scanner)
{
    private const int CiaAlign = 64;
    private const uint SigTypeRsa2048Sha256 = 0x00010004;
    private const int TicketSize = 0x140 + 0x164 + 0x28 + 0x84;
    private const int TmdBaseSize = 0x140 + 0xC4 + (0x24 * 64);
    private const int TmdChunkSize = 0x30;
    private const int MediaUnit = 0x200;
    private readonly string _id1Path = scanner.Id1Path;

    public event Action<string>? OnLog;
    public event Action<double, long, long>? OnProgress;

    public async Task ExtractToCiaAsync(InstalledTitle title, string outputPath, CancellationToken ct = default)
    {
        Log($"추출 중: {title.TitleId}");
        bool completed = false;

        try
        {
            outputPath = Utils.GetUniqueFilePath(outputPath);

            await using var output = File.Create(outputPath);

            await ExtractAsync(title, output, ct);

            completed = true;

            Log($"추출 완료: {outputPath}");
        }
        finally
        {
            if (!completed && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    private async Task ExtractAsync(InstalledTitle title, Stream output, CancellationToken ct)
    {
        string contentPath = title.ContentPath;
        string contentRootCmd = $"/title/{title.TitleIdHigh}/{title.TitleIdLow}/content";
        string tmdFile = Directory.GetFiles(contentPath, "*.tmd").FirstOrDefault() ?? throw new FileNotFoundException("TMD 파일을 찾을 수 없음");
        var (contents, tmd) = await BuildContentsAsync(title, ct);

        if (contents.Count == 0)
            throw new InvalidOperationException("추출 가능한 콘텐츠 없음");

        byte[]? exheader = null;
        byte[]? smdhData = null;
        var content0 = contents.FirstOrDefault(c => c.ContentIndex == 0);

        if (content0 != null)
        {
            await using var src0 = File.OpenRead(content0.FilePath);
            var sdDec = new SdDecryptStream(src0, content0.SdPath, sdCrypto);
            byte[] ncchHeaderBytes = new byte[0x200];

            await sdDec.ReadExactlyAsync(ncchHeaderBytes, 0, 0x200, ct);

            var ncch0 = NcchHeader.Parse(ncchHeaderBytes, 0);
            long exefsEnd = ncch0.ExefsOffset == 0 ? 0x200 : ((long)ncch0.ExefsOffset + ncch0.ExefsSize) * 0x200;
            int readSize = (int)Math.Min(exefsEnd, src0.Length);
            byte[] partial = new byte[readSize];

            sdDec.Position = 0;

            await sdDec.ReadExactlyAsync(partial, 0, readSize, ct);

            var ms = new MemoryStream(partial)
            {
                Position = 0
            };
            await using var dec0 = new NcchDecryptionStream(ms, 0, keyStore);

            if (ncch0.ExtendedHeaderSize > 0)
            {
                exheader = new byte[0x400];
                dec0.Position = 0x200;

                await dec0.ReadExactlyAsync(exheader, 0, 0x400, ct);
            }

            smdhData = await ReadExeFsIconAsync(dec0, ncch0, ct);
        }

        byte[] titleKey = RandomNumberGenerator.GetBytes(16);

        string certsPath = Path.Combine(AppContext.BaseDirectory, "certs.bin");

        if (!File.Exists(certsPath))
            throw new CertsBinNotFoundException("certs.bin 추출 필요 / 유틸 - certs.bin 추출을 진행하세요");

        byte[] certChain = await File.ReadAllBytesAsync(certsPath, ct);

        uint certChainSize = (uint)certChain.Length;
        uint ticketSize = TicketSize;
        uint tmdSize = (uint)(TmdBaseSize + TmdChunkSize * contents.Count);
        ulong contentSize = (ulong)contents.Sum(c => AlignUp(c.ContentSize, CiaAlign));

        WriteCiaHeader(output, contents, certChainSize, ticketSize, tmdSize, contentSize);

        long certOffset = AlignUp(0x2020, CiaAlign);

        output.Position = certOffset;

        await output.WriteAsync(certChain, ct);

        long ticketOffset = AlignUp(certOffset + certChainSize, CiaAlign);

        output.Position = ticketOffset;

        await output.WriteAsync(BuildTicket(tmd.TitleId, titleKey, contents), ct);

        long tmdOffset = AlignUp(ticketOffset + ticketSize, CiaAlign);
        byte[] tmdBuf = BuildTmd(tmd, contents, new byte[contents.Count][]);

        output.Position = tmdOffset;

        await output.WriteAsync(tmdBuf, ct);

        long firstContentOffset = AlignUp(tmdOffset + tmdSize, CiaAlign);

        output.Position = firstContentOffset;

        long totalBytes = contents.Sum(c => c.ContentSize);
        long written = 0;
        var contentHashes = new byte[contents.Count][];

        for (int i = 0; i < contents.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = contents[i];

            Log($"추출 중: {entry.ContentIdHex}.app");

            await using var src = File.OpenRead(entry.FilePath);
            Stream decrypted = new SdDecryptStream(src, entry.SdPath, sdCrypto);

            byte[] ncchHeaderBytes = new byte[0x200];

            await decrypted.ReadExactlyAsync(ncchHeaderBytes, 0, 0x200, ct);

            decrypted.Position = 0;

            var ncch = NcchHeader.Parse(ncchHeaderBytes, 0);

            if (!ncch.NoCrypto)
                decrypted = new NcchDecryptionStream(decrypted, 0, keyStore);

            await using (decrypted)
            {
                contentHashes[i] = await CopyWithHashAsync(decrypted, output, entry.ContentSize,
                    bytesWritten =>
                    {
                        written += bytesWritten;
                        OnProgress?.Invoke((double)written / totalBytes * 100, written, totalBytes);
                    }, ct);
            }

            long aligned = AlignUp(entry.ContentSize, CiaAlign);
            long padding = aligned - entry.ContentSize;

            if (padding > 0)
                await output.WriteAsync(new byte[padding], ct);
        }

        Log("TMD 해시 패치 중...");

        byte[] tmdFinal = BuildTmd(tmd, contents, contentHashes);

        output.Position = tmdOffset;

        await output.WriteAsync(tmdFinal, ct);

        output.Position = output.Length;

        byte[] meta = BuildMeta(smdhData, exheader);

        await output.WriteAsync(meta, ct);

        OnProgress?.Invoke(100, totalBytes, totalBytes);
    }

    public async Task ExtractToCciAsync(InstalledTitle title, string outputPath, CancellationToken ct = default)
    {
        Log($"CCI 추출 중: {title.TitleId}");
        bool completed = false;

        try
        {
            var (contents, _) = await BuildContentsAsync(title, ct);
            var source = new SdNcsdSource(contents, keyStore, sdCrypto);
            long totalSize = NcsdBuilder.CalculateOutputSize(source);
            long processedBytes = 0;

            var progress =
                new Progress<ProgressInfo>(p =>
                {
                    processedBytes = (long)(p.Percent / 100.0 * totalSize);
                    OnProgress?.Invoke(p.Percent, processedBytes, totalSize);
                });

            var reporter = new ProgressReporter(title.TitleId, title.TitleId, totalEstimated: 0, progress);

            outputPath = Utils.GetUniqueFilePath(outputPath);

            await using var output = File.Create(outputPath);
            await using (source)
            {
                await NcsdBuilder.BuildAsync(source, output, reporter.CreateAction(), ct);
            }

            completed = true;
            Log($"CCI 추출 완료: {outputPath}");
        }
        finally
        {
            if (!completed && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    private async Task<(List<Contents> contents, TmdHeader tmd)> BuildContentsAsync(InstalledTitle title, CancellationToken ct)
    {
        string contentPath = title.ContentPath;
        string tmdFile = Directory.GetFiles(contentPath, "*.tmd").FirstOrDefault() ?? throw new FileNotFoundException("TMD 파일을 찾을 수 없음");
        string tmdSdPath = ToSdPath(tmdFile);
        byte[] tmdEncrypted = await File.ReadAllBytesAsync(tmdFile, ct);
        byte[] tmdDecrypted = sdCrypto.Decrypt(tmdSdPath, tmdEncrypted);
        var tmd = TmdParser.Parse(tmdDecrypted);
        var contents = new List<Contents>();

        foreach (var record in tmd.Contents)
        {
            string filename = record.ContentIdHex + ".app";
            string subDir = $"{record.ContentIndex / 256:X8}";
            string dlcPath = Path.Combine(contentPath, subDir, filename);
            string normalPath = Path.Combine(contentPath, filename);
            string filePath;
            string sdPath;

            if (File.Exists(dlcPath))
            {
                filePath = dlcPath;
                sdPath = ToSdPath(dlcPath);
            }
            else if (File.Exists(normalPath))
            {
                filePath = normalPath;
                sdPath = ToSdPath(normalPath);
            }
            else
            {
                Log($"경고: 콘텐츠 파일 없음: {filename} (스킵)");
                continue;
            }

            contents.Add(new Contents
            {
                ContentId = record.ContentId,
                ContentIndex = record.ContentIndex,
                ContentType = record.ContentType,
                ContentSize = (long)Math.Min(new FileInfo(filePath).Length, record.ContentSize),
                Sha256Hash = record.Sha256Hash,
                FilePath = filePath,
                SdPath = sdPath,
            });
        }

        if (contents.Count == 0)
            throw new InvalidOperationException("추출 가능한 콘텐츠 없음");

        return (contents, tmd);
    }

    private static async Task<byte[]?> ReadExeFsIconAsync(Stream ncchStream, NcchHeader ncch, CancellationToken ct)
    {
        if (ncch.ExefsOffset == 0) 
            return null;

        long exefsStart = (long)ncch.ExefsOffset * 0x200;
        byte[] exefsHeader = new byte[0x200];
        ncchStream.Position = exefsStart;
        await ncchStream.ReadExactlyAsync(exefsHeader, 0, 0x200, ct);

        for (int i = 0; i < 8; i++)
        {
            int entryOff = i * 16;
            string name = System.Text.Encoding.ASCII.GetString(exefsHeader, entryOff, 8).TrimEnd('\0');
            uint sectionOffset = BitConverter.ToUInt32(exefsHeader, entryOff + 8);
            uint sectionSize = BitConverter.ToUInt32(exefsHeader, entryOff + 12);

            if (name == "icon" && sectionSize > 0)
            {
                byte[] iconData = new byte[sectionSize];
                ncchStream.Position = exefsStart + 0x200 + sectionOffset;
                await ncchStream.ReadExactlyAsync(iconData, 0, (int)sectionSize, ct);
                return iconData;
            }
        }

        return null;
    }

    private static async Task<byte[]> CopyWithHashAsync(Stream input, Stream output, long size, Action<long> onProgress, CancellationToken ct)
    {
        var pool = ArrayPool<byte>.Shared;
        byte[] buf = pool.Rent(1024 * 1024);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

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

                sha.AppendData(buf, 0, read);
                await output.WriteAsync(buf.AsMemory(0, read), ct);
                onProgress(read);
                remaining -= read;
            }
        }
        finally { pool.Return(buf); }

        return sha.GetCurrentHash();
    }

    private static void WriteCiaHeader(Stream output, List<Contents> contents, uint certChainSize, uint ticketSize, uint tmdSize, ulong contentSize)
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

        foreach (var entry in contents)
            buf[0x20 + entry.ContentIndex / 8] |= (byte)(0x80 >> (entry.ContentIndex & 7));

        output.Write(buf);
    }

    private byte[] BuildTicket(ulong titleId, byte[] titleKey, List<Contents> contents)
    {
        byte[] buf = new byte[TicketSize];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigTypeRsa2048Sha256);

        int h = 0x140;
        System.Text.Encoding.ASCII.GetBytes("Root-CA00000004-XS00000009").CopyTo(buf, h + 0x00);

        buf[h + 0x7C] = 0x01;

        byte[] encTitleKey = EncryptTitleKey(titleKey, titleId);
        encTitleKey.CopyTo(buf, h + 0x7F);

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

        foreach (var entry in contents)
            buf[idxData + 0x04 + (entry.ContentIndex & 0x3FF) / 8] |= (byte)(1 << (entry.ContentIndex & 0x7));

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

    private static byte[] BuildTmd(TmdHeader tmd, List<Contents> contents, byte[][] contentHashes)
    {
        int contentCount = contents.Count;
        byte[] buf = new byte[TmdBaseSize + TmdChunkSize * contentCount];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigTypeRsa2048Sha256);

        int hdr = 0x140;

        System.Text.Encoding.ASCII.GetBytes("Root-CA00000004-CP0000000a") .CopyTo(buf, hdr + 0x00);
        buf[hdr + 0x40] = 0x01;

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(hdr + 0x4C), tmd.TitleId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(hdr + 0x54), 0x00000040);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(hdr + 0x5A), tmd.SaveSize);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(hdr + 0x9C), tmd.TitleVersion);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(hdr + 0x9E), (ushort)contentCount);

        int infoOff = hdr + 0xC4;
        int chunkOff = infoOff + 0x24 * 64;

        for (int i = 0; i < contentCount; i++)
        {
            var entry = contents[i];
            int off = chunkOff + i * TmdChunkSize;

            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off + 0x00), entry.ContentId);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x04), entry.ContentIndex);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x06), 0x0000);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(off + 0x08), (ulong)entry.ContentSize);

            if (contentHashes[i] is { Length: 32 })
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

        if (smdhData != null)
        {
            int maxCopyLength = buf.Length - 0x400;
            int copyLength = Math.Min(smdhData.Length, maxCopyLength);

            Array.Copy(smdhData, 0, buf, 0x400, copyLength);
        }

        return buf;
    }

    private string ToSdPath(string absolutePath)
    {
        string id1 = _id1Path.TrimEnd('\\', '/');

        return absolutePath.StartsWith(id1, StringComparison.OrdinalIgnoreCase) ? absolutePath[id1.Length..].Replace('\\', '/') : absolutePath.Replace('\\', '/');
    }

    private static long AlignUp(long value, long align) => (value + align - 1) & ~(align - 1);

    private void Log(string msg) => OnLog?.Invoke(msg);
}