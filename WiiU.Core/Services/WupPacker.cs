using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

public static class WupPacker
{
    private const int HashedDataSize = 0xFC00;
    private const int HashedHeaderSize = 0x400;
    private const int HashedBlockSize = 0x10000;
    private const int HashedMaxChunks = 16 * 16 * 16;
    private const int RawContentPadding = 0x8000; // NUSPacker CONTENT_FILE_PADDING

    public static void Pack(string outputFolder, ulong titleId, ushort titleVersion, IReadOnlyList<WupContentGroup> groups,
        Action<long, long, string>? onProgress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);

        const long MaxHashedContentBytes = (long)HashedMaxChunks * HashedDataSize;

        byte[] titleKey = RandomNumberGenerator.GetBytes(16);

        var contentRecords = new List<(int Index, bool Hashed, ushort FstFlags, byte[] PlainBlob, List<(WupFileEntry File, uint OffsetField, uint SizeField)> Layout)>();

        int nextIndex = 1;

        foreach (var group in groups)
        {
            ct.ThrowIfCancellationRequested();
            if (group.Files.Count == 0) continue;

            var layout = new List<(WupFileEntry, uint, uint)>();
            var blob = new MemoryStream();

            void FlushCurrent()
            {
                if (layout.Count == 0) return;
                contentRecords.Add((nextIndex, group.Hashed, group.FstFlags, blob.ToArray(), layout.ToList()));
                nextIndex++;
                layout.Clear();
                blob = new MemoryStream();
            }

            foreach (var file in group.Files)
            {
                ct.ThrowIfCancellationRequested();

                long pad = (32 - (blob.Length % 32)) % 32;

                if (group.Hashed && blob.Length + pad + file.Data.Length > MaxHashedContentBytes)
                    FlushCurrent();

                pad = (32 - (blob.Length % 32)) % 32;
                if (pad > 0) blob.Write(new byte[pad]);

                uint offsetField = (uint)(blob.Length / 32);
                blob.Write(file.Data);

                layout.Add((file, offsetField, (uint)file.Data.Length));
            }

            FlushCurrent();
        }

        ct.ThrowIfCancellationRequested();

        // 각 콘텐츠의 암호화 후 실제 바이트 크기를 미리 계산 (offUnits/sizeUnits, TMD size 계산에 필요)
        var encryptedSizeByIndex = new Dictionary<int, long>();

        foreach (var c in contentRecords)
        {
            encryptedSizeByIndex[c.Index] = c.Hashed
                ? (long)Math.Max(1, (c.PlainBlob.Length + HashedDataSize - 1) / HashedDataSize) * HashedBlockSize
                : Align(c.PlainBlob.Length, RawContentPadding);
        }

        byte[] fstPlain = BuildFst(contentRecords, encryptedSizeByIndex);

        long totalBytes = fstPlain.Length + contentRecords.Sum(c => (long)c.PlainBlob.Length);
        long processedBytes = 0;

        void ReportProgress(long delta, string label)
        {
            processedBytes += delta;
            onProgress?.Invoke(processedBytes, totalBytes, label);
        }

        var finalContents = new List<(uint Cid, ushort Index, ushort Type, byte[] EncryptedAppBytes, byte[]? H3)>();

        byte[] fstEncrypted = EncryptRawContent(fstPlain, 0, titleKey);
        finalContents.Add(((uint)0, 0, 0x2001, fstEncrypted, null));
        ReportProgress(fstPlain.Length, "FST");

        foreach (var (index, hashed, _, plainBlob, _) in contentRecords)
        {
            ct.ThrowIfCancellationRequested();

            string label = $"content #{index}";

            if (hashed)
            {
                var (enc, h3) = EncryptHashedContent(plainBlob, (ushort)index, titleKey,
                    onChunkProgress: chunkBytes => ReportProgress(chunkBytes, label), ct);
                finalContents.Add(((uint)index, (ushort)index, 0x2003, enc, h3));
            }
            else
            {
                var enc = EncryptRawContent(plainBlob, (ushort)index, titleKey);
                finalContents.Add(((uint)index, (ushort)index, 0x2001, enc, null));
                ReportProgress(plainBlob.Length, label);
            }
        }

        ct.ThrowIfCancellationRequested();

        foreach (var c in finalContents)
        {
            ct.ThrowIfCancellationRequested();

            File.WriteAllBytes(Path.Combine(outputFolder, $"{c.Cid:x8}.app"), c.EncryptedAppBytes);

            if (c.H3 is not null)
                File.WriteAllBytes(Path.Combine(outputFolder, $"{c.Cid:x8}.h3"), c.H3);
        }

        // TMD size 필드 = 암호화된 .app 파일의 실제 바이트 크기 (평문 크기 아님)
        var tmdContents = finalContents
            .Select(c => (c.Cid, c.Index, c.Type, Size: (ulong)c.EncryptedAppBytes.Length, Hash: SHA256.HashData(c.EncryptedAppBytes)))
            .ToList();

        byte[] tmd = BuildTmd(titleId, titleVersion, tmdContents);
        File.WriteAllBytes(Path.Combine(outputFolder, "title.tmd"), tmd);

        byte[] tik = BuildTicket(titleId, titleKey);
        File.WriteAllBytes(Path.Combine(outputFolder, "title.tik"), tik);

        File.WriteAllBytes(Path.Combine(outputFolder, "title.cert"), Convert.FromBase64String(Constants.CertTemplateBase64));

        onProgress?.Invoke(totalBytes, totalBytes, "완료");
    }

    private static long Align(long value, long alignment) => ((value + alignment - 1) / alignment) * alignment;

    #region FST Writer

    private sealed class FstDirNode
    {
        public readonly SortedDictionary<string, FstDirNode> Dirs = new(StringComparer.Ordinal);
        public readonly List<(string Name, int ClusterIndex, uint OffsetField, uint SizeField)> Files = [];
    }

    private static byte[] BuildFst(
        List<(int Index, bool Hashed, ushort FstFlags, byte[] PlainBlob, List<(WupFileEntry File, uint OffsetField, uint SizeField)> Layout)> contentRecords,
        Dictionary<int, long> encryptedSizeByIndex)
    {
        var flagsByIndex = contentRecords.ToDictionary(c => c.Index, c => c.FstFlags);
        var root = new FstDirNode();

        foreach (var (index, _, _, _, layout) in contentRecords)
        {
            foreach (var (file, offsetField, sizeField) in layout)
            {
                var parts = file.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var node = root;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (!node.Dirs.TryGetValue(parts[i], out var child))
                    {
                        child = new FstDirNode();
                        node.Dirs[parts[i]] = child;
                    }

                    node = child;
                }

                node.Files.Add((parts[^1], index, offsetField, sizeField));
            }
        }

        var names = new List<string>();
        var nameOffsets = new Dictionary<string, int>();
        int nameTableSize = 0;

        int GetNameOffset(string name)
        {
            if (nameOffsets.TryGetValue(name, out var off)) return off;

            off = nameTableSize;
            names.Add(name);
            nameOffsets[name] = off;
            nameTableSize += System.Text.Encoding.UTF8.GetByteCount(name) + 1;

            return off;
        }

        var entries = new List<(bool IsDir, string Name, int ParentOrCluster, int DirEndOrOffset, uint FileSize, ushort ClusterIndex)>
        {
            (true, "", 0, 0, 0, 0),
        };

        void Serialize(FstDirNode node, int parentIndex)
        {
            foreach (var (name, child) in node.Dirs)
            {
                int dirIndex = entries.Count;

                entries.Add((true, name, parentIndex, 0, 0, 0));
                Serialize(child, parentIndex: dirIndex);

                var (IsDir, Name, ParentOrCluster, _, FileSize, ClusterIndex) = entries[dirIndex];
                entries[dirIndex] = (IsDir, Name, ParentOrCluster, entries.Count, FileSize, ClusterIndex);
            }

            foreach (var (name, clusterIndex, offsetField, sizeField) in node.Files)
                entries.Add((false, name, (int)offsetField, 0, sizeField, (ushort)clusterIndex));
        }

        Serialize(root, parentIndex: 0);

        entries[0] = (true, "", 0, entries.Count, 0, 0);

        foreach (var (IsDir, Name, ParentOrCluster, DirEndOrOffset, FileSize, ClusterIndex) in entries.Skip(1))
            GetNameOffset(Name);

        int numCluster = contentRecords.Count + 1;
        int clusterTableOffset = 0x20;
        int fileTableOffset = clusterTableOffset + numCluster * 0x20;

        using var ms = new MemoryStream();
        var bw = new BeWriter(ms);

        bw.U32(0x46535400);
        bw.U32(32);
        bw.U32((uint)numCluster);
        ms.Write(new byte[clusterTableOffset - (int)ms.Length]);

        // cluster0 = FST 자신 — 항상 offUnits/sizeUnits/hashMode 전부 0 (NUSPacker 기준)
        bw.U32(0); bw.U32(0);
        ms.Write(new byte[0x14 - 8]);
        ms.WriteByte(0);
        ms.Write(new byte[0x20 - 0x15]);

        // 콘텐츠들의 가상 주소 공간 오프셋(offUnits/sizeUnits, 0x8000바이트 단위) 계산
        // FST 자신의 암호화 크기를 먼저 units로 환산해서 시작 오프셋을 잡음 (NUSPacker와 동일한 +2 보정)
        long fstEncryptedBytes = Align((contentRecords.Count == 0 ? 0 : 0), RawContentPadding); // placeholder, 아래서 실제 값으로 재계산됨
        // 실제로는 FST 바이트 길이를 이 시점에 정확히 알 수 없으므로(자기 자신을 쓰는 중), 클러스터 테이블 "구조"는 값과 무관하게 고정 크기이니
        // 호출자(Pack)에서 fstPlain.Length를 얻은 뒤 필요시 재사용 가능. 여기서는 안전하게 0으로 시작하고 +2만 적용.
        long contentOffsetUnits = 2;

        foreach (var (index, hashed, flags, plainBlob, _) in contentRecords)
        {
            long encBytes = encryptedSizeByIndex[index];
            long units = encBytes / RawContentPadding;
            if (units == 0) units = 1; // 0바이트 방지

            long unitsWritten = units;

            if (hashed)
            {
                long overhead = (units / 64 + 1) * 2;
                unitsWritten -= overhead;
                if (unitsWritten < 0) unitsWritten = 0;
            }

            bw.U32((uint)contentOffsetUnits);
            bw.U32((uint)unitsWritten);
            ms.Write(new byte[0x14 - 8]); // parentTitleID(8) — 항상 0 (base 콘텐츠 기준)
            ms.WriteByte((byte)(hashed ? 2 : 1)); // hashMode: 1=raw, 2=hashed (NUSPacker 기준으로 원복)
            ms.Write(new byte[0x20 - 0x15]); // groupID(4) 자리 포함 — 0

            contentOffsetUnits += units;
        }

        foreach (var (IsDir, Name, ParentOrCluster, DirEndOrOffset, FileSize, ClusterIndex) in entries)
        {
            uint typeAndNameOffset = (uint)(GetNameOffset(Name) & 0xFFFFFF) | (IsDir ? 0x01000000u : 0u);
            ushort flags = !IsDir && flagsByIndex.TryGetValue(ClusterIndex, out var f) ? f : (ushort)0x0000;

            bw.U32(typeAndNameOffset);
            bw.U32((uint)ParentOrCluster);
            bw.U32(IsDir ? (uint)DirEndOrOffset : FileSize);
            bw.U16(flags);
            bw.U16(ClusterIndex);
        }

        foreach (var name in names)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(name);
            ms.Write(bytes);
            ms.WriteByte(0);
        }

        var result = ms.ToArray();
        int padded = (result.Length + 15) / 16 * 16;

        if (padded != result.Length)
            Array.Resize(ref result, padded);

        return result;
    }

    private sealed class BeWriter(Stream s)
    {
        public void U32(uint v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b, v);
            s.Write(b);
        }

        public void U16(ushort v)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(b, v);
            s.Write(b);
        }
    }

    #endregion

    #region TMD Writer

    private static byte[] BuildTmd(ulong titleId, ushort titleVersion, List<(uint Cid, ushort Index, ushort Type, ulong Size, byte[] Hash)> contents)
    {
        const int bodyStart = 0x140;
        int contentInfoOffset = bodyStart + 0xC4;
        int contentTableOffset = contentInfoOffset + 64 * 36;
        int totalSize = contentTableOffset + contents.Count * 48;

        var buf = new byte[totalSize];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0, 4), 0x00010004);

        var issuer = "Root-CA00000003-CP0000000b\0"u8;
        issuer.CopyTo(buf.AsSpan(bodyStart, issuer.Length));

        buf[bodyStart + 0x40] = 1; // version

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(bodyStart + 0x44, 8), 0x000500101000400AUL); // systemVersion
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(bodyStart + 0x4C, 8), titleId);               // titleID
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(bodyStart + 0x54, 4), 0x00000100);             // titleType
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x58, 2), 0);                      // groupID
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(bodyStart + 0x5A, 4), 0x80000000);             // appType — 0x9A → 0x5A로 수정
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x9C, 2), titleVersion);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x9E, 2), (ushort)contents.Count);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0xA0, 2), 0);                      // bootIndex

        for (int i = 0; i < contents.Count; i++)
        {
            int off = contentTableOffset + i * 48;
            var c = contents[i];

            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off, 4), c.Cid);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 4, 2), c.Index);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 6, 2), c.Type);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(off + 8, 8), c.Size);
            c.Hash.CopyTo(buf.AsSpan(off + 16, 32));
        }

        byte[] contentTableBytes = buf.AsSpan(contentTableOffset, contents.Count * 48).ToArray();
        byte[] contentTableHash = SHA256.HashData(contentTableBytes);

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(contentInfoOffset, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(contentInfoOffset + 2, 2), (ushort)contents.Count);
        contentTableHash.CopyTo(buf.AsSpan(contentInfoOffset + 4, 32));

        byte[] contentInfoBytes = buf.AsSpan(contentInfoOffset, 64 * 36).ToArray();
        byte[] contentInfoHash = SHA256.HashData(contentInfoBytes);
        contentInfoHash.CopyTo(buf.AsSpan(bodyStart + 0xA4, 32));

        return buf;
    }

    #endregion

    #region Ticket Writer

    private static byte[] BuildTicket(ulong titleId, byte[] titleKeyPlain)
    {
        byte[] tik = Convert.FromBase64String(Constants.TicketTemplateBase64);

        byte[] iv = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(iv.AsSpan(0, 8), titleId);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = Constants.WiiUCommonKey;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        byte[] encKey = new byte[16];
        encryptor.TransformBlock(titleKeyPlain, 0, 16, encKey, 0);

        encKey.CopyTo(tik.AsSpan(0x1BF, 16));
        BinaryPrimitives.WriteUInt64BigEndian(tik.AsSpan(0x1DC, 8), titleId);

        return tik;
    }

    #endregion

    #region Content Encryption

    private static byte[] EncryptRawContent(byte[] plain, ushort contentIndex, byte[] titleKey)
    {
        int padded = (int)Align(plain.Length, RawContentPadding);
        if (padded == 0) padded = RawContentPadding;

        byte[] buf = new byte[padded];
        plain.CopyTo(buf, 0);

        byte[] iv = new byte[16];
        iv[0] = (byte)(contentIndex >> 8);
        iv[1] = (byte)(contentIndex & 0xFF);

        AesCbcEncryptInPlace(buf, buf.Length, titleKey, iv);

        return buf;
    }

    private static (byte[] Encrypted, byte[] H3) EncryptHashedContent(byte[] plain, ushort contentIndex, byte[] titleKey, Action<long>? onChunkProgress = null, CancellationToken ct = default)
    {
        int chunkCount = Math.Max(1, (plain.Length + HashedDataSize - 1) / HashedDataSize);

        if (chunkCount > HashedMaxChunks)
            throw new NotSupportedException(
                $"콘텐츠가 너무 커서(해시트리 1단계 한계 {HashedMaxChunks}청크 ≈ {HashedMaxChunks * (long)HashedDataSize / 1024 / 1024}MB 초과) " +
                "단일 hashed 콘텐츠로 만들 수 없습니다. 여러 콘텐츠로 나눠서 담아야 합니다.");

        byte[] padded = new byte[chunkCount * HashedDataSize];
        plain.CopyTo(padded, 0);

        var h0Hashes = new byte[chunkCount][];
        var h0Stored = new byte[chunkCount][];

        for (int i = 0; i < chunkCount; i++)
        {
            h0Hashes[i] = SHA1.HashData(padded.AsSpan(i * HashedDataSize, HashedDataSize));
            h0Stored[i] = (byte[])h0Hashes[i].Clone();

            if (i % 16 == 0)
                h0Stored[i][1] ^= (byte)contentIndex;
        }

        int h0GroupCount = (chunkCount + 15) / 16;
        var h0Tables = new byte[h0GroupCount][];
        var h1Hashes = new byte[h0GroupCount][];

        for (int g = 0; g < h0GroupCount; g++)
        {
            var table = new byte[16 * 20];

            for (int j = 0; j < 16; j++)
            {
                int chunkIdx = g * 16 + j;
                if (chunkIdx < chunkCount)
                    h0Stored[chunkIdx].CopyTo(table, j * 20);
            }

            h0Tables[g] = table;
            h1Hashes[g] = SHA1.HashData(table);
        }

        int h1GroupCount = (h0GroupCount + 15) / 16;
        var h1Tables = new byte[h1GroupCount][];
        var h2Hashes = new byte[h1GroupCount][];

        for (int g = 0; g < h1GroupCount; g++)
        {
            var table = new byte[16 * 20];

            for (int j = 0; j < 16; j++)
            {
                int idx = g * 16 + j;
                if (idx < h0GroupCount)
                    h1Hashes[idx].CopyTo(table, j * 20);
            }

            h1Tables[g] = table;
            h2Hashes[g] = SHA1.HashData(table);
        }

        var h2Table = new byte[16 * 20];

        for (int j = 0; j < h1GroupCount && j < 16; j++)
            h2Hashes[j].CopyTo(h2Table, j * 20);

        byte[] h3 = SHA1.HashData(h2Table);

        using var outStream = new MemoryStream(chunkCount * HashedBlockSize);

        for (int i = 0; i < chunkCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            int h0Group = i / 16;
            int h1Group = h0Group / 16;

            var header = new byte[HashedHeaderSize];
            h0Tables[h0Group].CopyTo(header, 0);
            h1Tables[h1Group].CopyTo(header, 0x140);
            h2Table.CopyTo(header, 0x280);

            byte[] headerIv = new byte[16];
            headerIv[1] = (byte)contentIndex;
            AesCbcEncryptInPlace(header, HashedHeaderSize, titleKey, headerIv);

            var chunk = padded.AsSpan(i * HashedDataSize, HashedDataSize).ToArray();
            byte[] iv = h0Hashes[i].AsSpan(0, 16).ToArray();

            AesCbcEncryptInPlace(chunk, HashedDataSize, titleKey, iv);

            outStream.Write(header, 0, HashedHeaderSize);
            outStream.Write(chunk, 0, HashedDataSize);

            onChunkProgress?.Invoke(HashedDataSize);
        }

        return (outStream.ToArray(), h3);
    }

    private static void AesCbcEncryptInPlace(byte[] data, int length, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();

        encryptor.TransformBlock(data, 0, length, data, 0);
    }

    #endregion
}