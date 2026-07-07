using _3DS.Core.Crypto;
using _3DS.Core.FileSystem;
using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using _3DS.Core.Save;
using _3DS.Core.Save.Enums;
using Common;
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public class TitleInstaller(KeyStore keyStore, SdCrypto sdCrypto, SdTitleScanner scanner)
{
    private const int TitleAlignSize = 0x8000;
    private readonly string _id1Path = scanner.Id1Path;

    public event Action<string>? OnLog;

    public Task InstallAsync(CiaSource cia, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => InstallCoreAsync(cia, cia.TmdRaw, (idx) => cia.OpenContentNcchEncrypted(idx), progress, ct);

    public Task InstallAsync(CciSource cci, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => InstallCoreAsync(cci, cci.TmdRaw, (idx) => cci.OpenContentDecrypted(idx), progress, ct);

    private async Task InstallCoreAsync(IInstallSource source, byte[] tmdRaw, Func<int, ValueTask<(Stream stream, long size)>> openContent, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var tmd = source.TmdHeader;
        ulong titleId = tmd.TitleId;
        string tidHigh = $"{(titleId >> 32):x8}";
        string tidLow = $"{(titleId & 0xFFFFFFFF):x8}";
        bool isDlc = (titleId >> 32) == 0x0004008C;
        bool hasSave = tmd.SaveSize > 0;
        string titleRoot = Path.Combine(_id1Path, "title", tidHigh, tidLow);
        string contentRoot = Path.Combine(titleRoot, "content");
        string cmdRoot = Path.Combine(contentRoot, "cmd");
        string titleRootCmd = $"/title/{tidHigh}/{tidLow}";
        string contentRootCmd = titleRootCmd + "/content";

        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(cmdRoot);

        if (isDlc)
        {
            for (int x = 0; x <= (tmd.Contents.Length - 1) / 256; x++)
                Directory.CreateDirectory(Path.Combine(contentRoot, $"{x:x8}"));
        }

        int tmdId = 0;
        string tmdFilename = $"{tmdId:x8}.tmd";
        string tmdSdPath = contentRootCmd + "/" + tmdFilename;
        string tmdOutputPath = Path.Combine(contentRoot, tmdFilename);

        Log($"TMD 저장: {tmdSdPath}");
        await WriteEncryptedAsync(tmdOutputPath, tmdSdPath, tmdRaw, ct);

        long totalBytes = tmd.Contents.Sum(c => c.ContentSize);
        long written = 0;

        var reporter = progress is null ? null : new ProgressReporter("설치 중...", string.Empty, totalBytes, progress);
        var report = reporter?.CreateAction();

        for (int i = 0; i < tmd.Contents.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var content = tmd.Contents[i];

            if (!source.IsContentPresent(content.ContentIndex))
            {
                Log($"경고: 콘텐츠 인덱스 {content.ContentIndex} 미포함, 건너뜀");
                continue;
            }

            string contentFilename = $"{content.ContentId:x8}.app";
            string contentSdPath, contentOutputPath;

            if (isDlc)
            {
                string dirIndex = $"{content.ContentIndex / 256:x8}";

                contentSdPath = contentRootCmd + $"/{dirIndex}/{contentFilename}";
                contentOutputPath = Path.Combine(contentRoot, dirIndex, contentFilename);
            }
            else
            {
                contentSdPath = contentRootCmd + "/" + contentFilename;
                contentOutputPath = Path.Combine(contentRoot, contentFilename);
            }

            Log($"콘텐츠 저장: {contentSdPath}");

            var (ncchStream, contentSize) = await openContent(content.ContentIndex);

            await using (ncchStream)
            {
                byte[] hash = await CopyEncryptedAsync(ncchStream, contentOutputPath, contentSdPath, contentSize,
                    bytesWritten =>
                    {
                        written += bytesWritten;
                        report?.Invoke(written, totalBytes);
                    }, ct);

                if (!hash.AsSpan().SequenceEqual(content.Sha256Hash))
                    Log($"경고: 해시 불일치 {contentFilename}");
            }
        }

        if (hasSave)
        {
            string savSdPath = titleRootCmd + "/data/00000001.sav";
            string savOutPath = Path.Combine(titleRoot, "data", "00000001.sav");

            Directory.CreateDirectory(Path.GetDirectoryName(savOutPath)!);

            if (!File.Exists(savOutPath))
            {
                Log($"빈 세이브 생성: {savSdPath}");

                byte[] encZeros = sdCrypto.Encrypt(savSdPath, new byte[0x20]);
                await using var savFile = File.Create(savOutPath);
                await savFile.WriteAsync(encZeros, ct);
                await savFile.WriteAsync(new byte[tmd.SaveSize - 0x20], ct);
            }
        }

        int cmdId = isDlc ? tmd.Contents.Length : 1;
        string cmdFilename = $"{cmdId:x8}.cmd";
        string cmdSdPath = contentRootCmd + "/cmd/" + cmdFilename;
        string cmdOutputPath = Path.Combine(cmdRoot, cmdFilename);

        Log($"CMD 생성: {cmdSdPath}");

        byte[] cmdData = await BuildCmdAsync(source, cmdId, ct);

        await WriteEncryptedAsync(cmdOutputPath, cmdSdPath, cmdData, ct);

        Log("title.db 업데이트...");
        await UpdateTitleDbAsync(source, titleId, cmdId, isDlc, hasSave, ct);

        byte[]? seed = keyStore.GetSeed(titleId);
        var finish = new CiFinishManager(scanner.SdRoot);

        finish.AddOrUpdate(titleId, seed);

        const string homebrewFileName = "custom-install-finalize.3dsx";
        string homebrewDirectory = Path.Combine(scanner.SdRoot, "3ds");
        string homebrewPath = Path.Combine(homebrewDirectory, homebrewFileName);

        if (!Directory.Exists(homebrewDirectory))
            Directory.CreateDirectory(homebrewDirectory);

        if (!File.Exists(homebrewPath))
            await File.WriteAllBytesAsync(homebrewPath, Properties.Resources.custom_install_finalize, ct);

        report?.Invoke(totalBytes, totalBytes);
    }

    private async Task<byte[]> BuildCmdAsync(IInstallSource source, int cmdId, CancellationToken ct)
    {
        var tmd = source.TmdHeader;
        var contentIds = new Dictionary<int, (byte[] idBytes, byte[] cmac)>();
        int highestIndex = 0;

        foreach (var content in tmd.Contents)
        {
            if (!source.IsContentPresent(content.ContentIndex))
                continue;

            highestIndex = Math.Max(highestIndex, content.ContentIndex);

            var (ncchStream, _) = await source.OpenContentNcchEncrypted(content.ContentIndex);
            byte[] cmacData;

            await using (ncchStream)
            {
                ncchStream.Position = 0x100;

                byte[] ncchPart = new byte[0x100];

                await ncchStream.ReadExactlyAsync(ncchPart, 0, 0x100, ct);

                byte[] idBytes = new byte[4];

                BinaryPrimitives.WriteUInt32LittleEndian(idBytes, content.ContentId);

                byte[] indexBytes = new byte[4];

                BinaryPrimitives.WriteUInt32LittleEndian(indexBytes, (uint)content.ContentIndex);

                cmacData = new byte[ncchPart.Length + indexBytes.Length + idBytes.Length];
                ncchPart.CopyTo(cmacData, 0);
                indexBytes.CopyTo(cmacData, ncchPart.Length);
                idBytes.CopyTo(cmacData, ncchPart.Length + indexBytes.Length);
            }

            byte[] hash = SHA256.HashData(cmacData);
            byte[] cmac = SdCrypto.AesCmac(keyStore.GetNormalKey(0x30), hash);
            byte[] idBytesLE = new byte[4];

            BinaryPrimitives.WriteUInt32LittleEndian(idBytesLE, content.ContentId);

            contentIds[content.ContentIndex] = (idBytesLE, cmac);
        }

        var idsByIndex = new byte[highestIndex + 1][];
        var installedIds = new List<byte[]>();
        var cmacs = new List<byte[]>();
        byte[] missingCmac = System.Text.Encoding.ASCII.GetBytes("MISSING CONTENT!");

        for (int x = 0; x <= highestIndex; x++)
        {
            if (contentIds.TryGetValue(x, out var info))
            {
                idsByIndex[x] = info.idBytes;
                installedIds.Add(info.idBytes);
                cmacs.Add(info.cmac);
            }
            else
            {
                idsByIndex[x] = [0xFF, 0xFF, 0xFF, 0xFF];
                cmacs.Add(missingCmac);
            }
        }

        installedIds.Sort((a, b) =>
        {
            uint va = BinaryPrimitives.ReadUInt32LittleEndian(a);
            uint vb = BinaryPrimitives.ReadUInt32LittleEndian(b);

            return va.CompareTo(vb);
        });

        byte[] header = new byte[16];

        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), (uint)cmdId);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)idsByIndex.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)installedIds.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), 1);

        byte[] headerCmac = SdCrypto.AesCmac(keyStore.GetNormalKey(0x30), header);
        using var ms = new MemoryStream();

        await ms.WriteAsync(header, ct);
        await ms.WriteAsync(headerCmac, ct);

        foreach (var id in idsByIndex)
            await ms.WriteAsync(id, ct);

        foreach (var id in installedIds)
            await ms.WriteAsync(id, ct);

        foreach (var c in cmacs) await
                ms.WriteAsync(c, ct);

        return ms.ToArray();
    }

    private async Task UpdateTitleDbAsync(IInstallSource source, ulong titleId, int cmdId, bool isDlc, bool hasSave, CancellationToken ct)
    {
        string dbPath = Path.Combine(_id1Path, "dbs", "title.db");
        string dbSdPath = "/dbs/title.db";
        byte[] encrypted = await File.ReadAllBytesAsync(dbPath, ct);
        byte[] decrypted = sdCrypto.Decrypt(dbSdPath, encrypted);
        var memFile = new MemoryFile(decrypted);
        var db = new Db(memFile, DbType.SdTitle, keyStore.GetNormalKey(0x30));
        long titleSize = source.TmdHeader.Contents.Sum(c => c.ContentSize);
        byte[] entry = await BuildTitleInfoEntry(source, cmdId, hasSave, isDlc);
        var root = db.OpenRoot();

        try
        {
            var existing = root.OpenSubFile(titleId);

            existing.Delete();
        }
        catch { }

        var file = root.NewSubFile(titleId, entry.Length);

        file.Write(0, entry, 0, entry.Length);
        db.Commit();
        sdCrypto.EncryptToFile(dbPath, dbSdPath, decrypted);
    }

    private static async Task<byte[]> BuildTitleInfoEntry(IInstallSource source, int cmdId, bool hasSave, bool isDlc)
    {
        var tmd = source.TmdHeader;
        long titleSize = CalculateTitleSize(tmd);
        byte[] extdataId = new byte[8];
        ushort ncchVersion = 0;
        string productCode = string.Empty;
        bool hasManual = !isDlc && tmd.Contents.Any(c => c.ContentIndex == 1);

        try
        {
            var firstContent = tmd.Contents.OrderBy(c => c.ContentIndex).First();
            var ncch = await source.GetNcchHeaderAsync(firstContent.ContentIndex, CancellationToken.None);

            ncchVersion = ncch.Version;
            productCode = ncch.ProductCodeString;
        }
        catch { }

        byte[] buf = new byte[0x80];
        int pos = 0;

        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(pos), (ulong)titleSize);
        pos += 8;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), 0x40);
        pos += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(pos), tmd.TitleVersion);
        pos += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(pos), ncchVersion);
        pos += 2;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), hasManual ? 1u : 0u);
        pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), 0);
        pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), (uint)cmdId);
        pos += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), hasSave ? 1u : 0u);
        pos += 4;
        extdataId.AsSpan(0, 4).CopyTo(buf.AsSpan(pos)); pos += 4;
        pos += 4;
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(pos), 0x100000000UL);
        pos += 8;
        System.Text.Encoding.ASCII.GetBytes(productCode.PadRight(0x10, '\0')[..Math.Min(0x10, productCode.Length)]).CopyTo(buf, pos);
        pos += 0x10;
        pos += 0x10;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos), (uint)Random.Shared.Next());
        pos += 4;

        return buf;
    }

    private static long CalculateTitleSize(TmdHeader tmd)
    {
        List<long> sizes =
        [
            .. Enumerable.Repeat(1L, 5),
        ];

        if (tmd.SaveSize > 0)
        {
            sizes.Add(1);
            sizes.Add(tmd.SaveSize);
        }

        sizes.AddRange(tmd.Contents.Select(c => c.ContentSize));

        return sizes.Sum(x => RoundUp(x, TitleAlignSize));
    }

    private static long RoundUp(long value, long align) => ((value + align - 1) / align) * align;

    private async Task WriteEncryptedAsync(string outputPath, string sdPath, byte[] data, CancellationToken ct)
    {
        byte[] encrypted = sdCrypto.Encrypt(sdPath, data);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllBytesAsync(outputPath, encrypted, ct);
    }

    private async Task<byte[]> CopyEncryptedAsync(Stream input, string outputPath, string sdPath, long size, Action<long> onProgress, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var outFile = File.Create(outputPath);
        var pool = ArrayPool<byte>.Shared;
        byte[] buf = pool.Rent(1024 * 1024);
        long remaining = size;
        byte[] key = sdCrypto.GetKey();
        byte[] iv = SdCrypto.PathToIv(sdPath);
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] ctr = (byte[])iv.Clone();
        byte[] keystream = new byte[16];
        byte[] ctrBuf = new byte[16];

        try
        {
            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                int toRead = (int)Math.Min(buf.Length, remaining);
                int read = await input.ReadAsync(buf.AsMemory(0, toRead), ct);

                if (read == 0)
                    break;

                sha.AppendData(buf, 0, read);

                int pos = 0;

                while (pos < read)
                {
                    Buffer.BlockCopy(ctr, 0, ctrBuf, 0, 16);
                    encryptor.TransformBlock(ctrBuf, 0, 16, keystream, 0);
                    int chunk = Math.Min(16, read - pos);
                    for (int i = 0; i < chunk; i++)
                        buf[pos + i] ^= keystream[i];
                    SdCrypto.IncrementCtr(ctr);
                    pos += chunk;
                }

                await outFile.WriteAsync(buf.AsMemory(0, read), ct);
                onProgress(read);
                remaining -= read;
            }
        }
        finally
        {
            pool.Return(buf);
        }

        return sha.GetCurrentHash();
    }

    private void Log(string msg) => OnLog?.Invoke(msg);
}