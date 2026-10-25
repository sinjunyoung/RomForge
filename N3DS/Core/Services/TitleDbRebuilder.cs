using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using N3DS.Core.Enums;
using N3DS.Core.FileSystem;
using N3DS.Core.Crypto;
using N3DS.Core.Models;
using N3DS.Save;
using N3DS.Core.IO;
using N3DS.Save.Enums;
using N3DS.Save.Interfaces;

namespace N3DS.Core.Services;

public class TitleDbRebuilder(KeyStore keyStore, SdCrypto sdCrypto, SdTitleScanner scanner)
{
    private const long AlignSize = 0x8000;
    private const uint NcchMagic = 0x4843434E;

    public event Action<string>? OnLog;

    public Task<(int Rebuilt, int Skipped)> RebuildAsync(Action<int, int>? onProgress = null, CancellationToken ct = default) => Task.Run(() => Rebuild(onProgress, ct), ct);

    private (int Rebuilt, int Skipped) Rebuild(Action<int, int>? onProgress, CancellationToken ct)
    {
        var tmdFiles = EnumerateTmdFiles().ToList();
        var entries = new SortedDictionary<ulong, byte[]>();
        int skipped = 0;

        for (int i = 0; i < tmdFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = TryBuildEntry(tmdFiles[i], out ulong titleId);

            if (entry is null)
                skipped++;
            else
                entries[titleId] = entry;

            onProgress?.Invoke(i + 1, tmdFiles.Count);
        }

        if (tmdFiles.Count > 0 && entries.Count == 0)
            throw new InvalidOperationException("TMD를 하나도 읽지 못했습니다. movable.sed가 이 SD 카드의 것인지 확인하세요.");

        byte[] key = keyStore.GetNormalKey(0x30);
        byte[] titleDb = CreateCleanDb(DbType.SdTitle);
        var db = new Db(new MemoryFile(titleDb), DbType.SdTitle, key);
        var root = db.OpenRoot();

        foreach (var (titleId, entry) in entries)
        {
            var file = root.NewSubFile(titleId, entry.Length);

            file.Write(0, entry, 0, entry.Length);
        }

        db.Commit();

        int stored = new Db(new MemoryFile(titleDb), DbType.SdTitle, key).OpenRoot().ListSubFile().Count;

        if (stored != entries.Count)
            throw new InvalidDataException($"title.db 검증 실패: 기대 {entries.Count}개, 실제 {stored}개");

        string dbsDir = Path.Combine(scanner.Id1Path, "dbs");

        Directory.CreateDirectory(dbsDir);
        WriteFile(Path.Combine(dbsDir, "import.db"), sdCrypto.Encrypt("/dbs/import.db", CreateCleanDb(DbType.SdImport)));
        WriteFile(Path.Combine(dbsDir, "title.db"), sdCrypto.Encrypt("/dbs/title.db", titleDb));

        return (entries.Count, skipped);
    }

    private byte[] CreateCleanDb(DbType type)
    {
        using var resource = typeof(TitleDbRebuilder).Assembly.GetManifestResourceStream("title.db.gz") ?? throw new InvalidOperationException("title.db.gz 리소스를 찾을 수 없습니다.");
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var ms = new MemoryStream();

        gzip.CopyTo(ms);

        byte[] plain = ms.ToArray();
        var signer = (ISigner)new DbSigner(type == DbType.SdImport ? 3u : 2u);
        byte[] cmac = SdCrypto.AesCmac(keyStore.GetNormalKey(0x30), signer.Hash(plain[0x100..0x200]));

        cmac.CopyTo(plain, 0);

        return plain;
    }

    private static void WriteFile(string path, byte[] data)
    {
        string temp = path + ".tmp";

        using (var fs = File.Create(temp))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(true);
        }

        File.Move(temp, path, overwrite: true);
    }

    private IEnumerable<string> EnumerateTmdFiles()
    {
        string titleRoot = Path.Combine(scanner.Id1Path, "title");

        if (!Directory.Exists(titleRoot))
            yield break;

        foreach (var highDir in Directory.EnumerateDirectories(titleRoot).Where(d => IsHex(Path.GetFileName(d), 8)).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var lowDir in Directory.EnumerateDirectories(highDir).Where(d => IsHex(Path.GetFileName(d), 8)).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string contentDir = Path.Combine(lowDir, "content");

                if (!Directory.Exists(contentDir))
                    continue;

                var tmds = Directory.EnumerateFiles(contentDir, "*.tmd")
                                    .Where(f => IsHex(Path.GetFileNameWithoutExtension(f), 8))
                                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                foreach (var tmd in tmds)
                    yield return tmd;
            }
        }
    }

    private byte[]? TryBuildEntry(string tmdPath, out ulong titleId)
    {
        string contentDir = Path.GetDirectoryName(tmdPath)!;
        string lowDir = Directory.GetParent(contentDir)!.FullName;
        string highDir = Directory.GetParent(lowDir)!.FullName;

        titleId = ulong.Parse(Path.GetFileName(highDir) + Path.GetFileName(lowDir), NumberStyles.HexNumber);

        uint tmdId = uint.Parse(Path.GetFileNameWithoutExtension(tmdPath), NumberStyles.HexNumber);
        string label = $"{titleId:x16}";
        TmdHeader tmd;

        try
        {
            tmd = TmdParser.Parse(sdCrypto.Decrypt(ToSdPath(tmdPath), File.ReadAllBytes(tmdPath)));
        }
        catch (Exception ex)
        {
            Log($"TMD 읽기 실패 ({label}): {ex.Message}");
            return null;
        }

        if (tmd.TitleId != titleId || tmd.Contents.Length == 0)
        {
            Log($"TMD가 올바르지 않습니다 ({label}): 건너뜀");
            return null;
        }

        bool isDlc = (tmd.TitleId >> 32) == (uint)TitleType.DlcContent;
        string appName = $"{tmd.Contents[0].ContentId:x8}.app";
        string appPath = isDlc ? Path.Combine(contentDir, "00000000", appName) : Path.Combine(contentDir, appName);
        ushort ncchVersion;
        string productCode;
        byte[] extdata;

        try
        {
            (ncchVersion, productCode, extdata) = ReadNcch(appPath, label);
        }
        catch (Exception ex)
        {
            Log($"메인 콘텐츠 읽기 실패 ({label}): {ex.Message}");
            return null;
        }

        long titleSize = CalculateSize(lowDir, out uint? cmdId);

        if (cmdId is null)
        {
            Log($"cmd 파일이 없습니다 ({label}): 건너뜀");
            return null;
        }

        bool hasManual = !isDlc && tmd.Contents.Any(c => c.ContentIndex == 1);
        byte[] buf = new byte[0x80];

        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x00), (ulong)titleSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x08), 0x40);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0C), tmd.TitleVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0E), ncchVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x10), hasManual ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x14), tmdId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x18), cmdId.Value);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x1C), tmd.SaveSize > 0 ? 1u : 0u);
        extdata.CopyTo(buf, 0x20);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x28), 0x100000000UL);
        Encoding.ASCII.GetBytes(productCode, 0, Math.Min(productCode.Length, 0x10), buf, 0x30);
        Random.Shared.NextBytes(buf.AsSpan(0x50, 4));

        return buf;
    }

    private (ushort Version, string ProductCode, byte[] Extdata) ReadNcch(string appPath, string label)
    {
        using var file = File.OpenRead(appPath);
        using var sd = new SdDecryptStream(file, ToSdPath(appPath), sdCrypto, leaveOpen: true);

        byte[] buf = new byte[0x600];
        int read = sd.ReadAtLeast(buf, buf.Length, throwOnEndOfStream: false);

        if (read < NcchHeader.Size)
            throw new InvalidDataException("NCCH 헤더가 너무 짧습니다.");

        var header = NcchHeader.Parse(buf, 0);

        if (header.Magic != NcchMagic)
            throw new InvalidDataException("NCCH 매직 번호가 일치하지 않습니다.");

        byte[] extdata = new byte[4];

        if (header.ExtendedHeaderSize == 0)
            return (header.Version, header.ProductCodeString, extdata);

        byte[]? exHeader = read >= buf.Length ? ResolveExHeader(buf.AsSpan(0x200, 0x400).ToArray(), header) : null;

        if (exHeader is null)
            Log($"경고: extdata ID를 확인하지 못해 0으로 기록합니다 ({label})");
        else
            Array.Copy(exHeader, 0x230, extdata, 0, 4);

        return (header.Version, header.ProductCodeString, extdata);
    }

    private byte[]? ResolveExHeader(byte[] raw, NcchHeader header)
    {
        bool Matches(byte[] data) => SHA256.HashData(data).AsSpan().SequenceEqual(header.ExtendedHeaderHash);

        if (header.NoCrypto || Matches(raw))
            return Matches(raw) ? raw : null;

        try
        {
            byte[] key = header.FixedKey ? new byte[16] : keyStore.DeriveNormalKey(0x2C, header.Signature[..16]);
            byte[] decrypted = AesCtr(raw, key, BuildExHeaderCtr(header));

            return Matches(decrypted) ? decrypted : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static byte[] BuildExHeaderCtr(NcchHeader header)
    {
        byte[] ctr = new byte[16];
        byte[] partitionId = new byte[8];

        BinaryPrimitives.WriteUInt64LittleEndian(partitionId, header.PartitionId);

        if (header.Version is 0 or 2)
        {
            for (int i = 0; i < 8; i++)
                ctr[i] = partitionId[7 - i];

            ctr[8] = 1;
        }
        else
        {
            partitionId.CopyTo(ctr, 0);
            BinaryPrimitives.WriteUInt32BigEndian(ctr.AsSpan(12), 0x200);
        }

        return ctr;
    }

    private static byte[] AesCtr(byte[] data, byte[] key, byte[] initialCtr)
    {
        byte[] result = new byte[data.Length];
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] ctr = (byte[])initialCtr.Clone();
        byte[] keystream = new byte[16];

        for (int offset = 0; offset < data.Length; offset += 16)
        {
            encryptor.TransformBlock(ctr, 0, 16, keystream, 0);

            int chunk = Math.Min(16, data.Length - offset);

            for (int i = 0; i < chunk; i++)
                result[offset + i] = (byte)(data[offset + i] ^ keystream[i]);

            SdCrypto.IncrementCtr(ctr);
        }

        return result;
    }

    private static long CalculateSize(string titleLowDir, out uint? cmdId)
    {
        long total = RoundUp(1);

        cmdId = null;

        foreach (var info in new DirectoryInfo(titleLowDir).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            string name = info.Name;

            if (name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) && name.Length >= 8 && uint.TryParse(name.AsSpan(0, 8), NumberStyles.HexNumber, null, out uint id))
                cmdId = cmdId is null ? id : Math.Max(cmdId.Value, id);

            if (name.StartsWith('.'))
                continue;

            bool isDir = info is DirectoryInfo;

            if (isDir && name.Length % 2 == 0 && name.All(Uri.IsHexDigit))
                continue;

            total += RoundUp(isDir ? 1 : ((FileInfo)info).Length);
        }

        return total;
    }

    private static long RoundUp(long value) => (value + AlignSize - 1) / AlignSize * AlignSize;

    private static bool IsHex(string s, int length) => s.Length == length && s.All(Uri.IsHexDigit);

    private string ToSdPath(string absolutePath) => "/" + Path.GetRelativePath(scanner.Id1Path, absolutePath).Replace('\\', '/');

    private void Log(string msg) => OnLog?.Invoke(msg);
}