using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WiiU.Core.Services;

/// <summary>WUP에 들어갈 파일 하나 (경로 + 평문 데이터).</summary>
public sealed record WupFileEntry(string RelativePath, byte[] Data);

/// <summary>콘텐츠 하나로 묶일 파일들의 그룹. code/meta처럼 파일 하나당 콘텐츠 하나를 원하면 Files에 1개만 넣고,
/// content/처럼 여러 파일을 한 콘텐츠에 오프셋으로 묶고 싶으면 여러 개를 넣으면 된다.</summary>
public sealed class WupContentGroup
{
    public required bool Hashed { get; init; }

    public required List<WupFileEntry> Files { get; init; }
}

/// <summary>
/// WUP(title.tmd/title.tik/title.cert + NNNNNNNN.app[+.h3]) 작성기.
///
/// 실제 NUSPacker(오픈소스, ihaveamac/nuspacker) 결과물을 실측 검증해서 만든 구현:
/// - TMD 필드 레이아웃은 NUSPacker TMD.java 소스와 바이트 단위로 대조 완료
/// - 티켓/인증서는 NUSPacker가 실제로 만든 파일을 템플릿으로 삼아 title_id / 암호화된 타이틀키만 패치
///   (서명, 라이선스 타입 등 나머지 필드는 실기에서 검증된 값 그대로 유지 — 실기는 시그니처 패치가
///   서명 검증 자체를 꺼버리므로 서명값은 0으로 채워도 무방함)
/// - FST/콘텐츠 암호화(raw AES-CBC, 해시트리 H0~H3)는 WupTitleSource의 읽기 로직을 실측 검증 후 그대로 반전
/// </summary>
public static class WupPacker
{
    private const int RawIvContentIndexZero = 0; // placeholder, IV는 콘텐츠 index로 매 콘텐츠마다 계산

    private const int HashedDataSize = 0xFC00;
    private const int HashedHeaderSize = 0x400;
    private const int HashedBlockSize = 0x10000; // HashedHeaderSize + HashedDataSize
    private const int HashedMaxChunks = 16 * 16 * 16; // H0(16)*H1(16)*H2(16) 1단계 트리 한계 (~252MB/콘텐츠)

    private static readonly byte[] WiiUCommonKey =
    [
        0xD7, 0xB0, 0x04, 0x02, 0x65, 0x9B, 0xA2, 0xAB,
        0xD2, 0xCB, 0x0D, 0xB2, 0x7F, 0xA2, 0xB6, 0x56
    ];

    // NUSPacker가 실제로 생성한 title.tik를 템플릿으로 사용. title_id(0x1DC~0x1E3)와
    // 암호화된 타이틀키(0x1BF~0x1CE)만 매 타이틀마다 새로 채워 넣는다.
    private const string TicketTemplateBase64 =
        "AAEABMM+GoDXSsUlw0mDGSHYYriW50KPm+2/6wZc2osi2RSeLbT8BckIHTZq2aIb4jomq4r7AacPsjhoSaLFR0m4o/6jANpom0p/" +
        "C2fC2p+BdSu4lEnrQje54/o77FPqP8ReXSSRJFzf7mjZVF+OYTgA+4Kg+E8/JuC6040+ViuKIppoSARdSrDJASrqca1YQbjwhxnN" +
        "zTqJJgDkwrZtncM9CS5MsYnFNEzE6QNsRSWRULvaGPHDM15/KJ9zwbBEa0BOJut41f6UNQRrlNfJ/4tL2MgGssO6DCtEViwTnjAW" +
        "lkoLLWNyrUHA6REYMkGtp9Lftd4SDrX0wBhXHOHQcUiwSH0AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAABSb290LUNBMDAwMDAwMDMtWFMwMDAwMDAwYwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAiSXa4SkSmNT/4ZN6y1j+yQAABQ" +
        "LISQSxRQAAAAAABQAAEjRWeAAAABEAAAAAAAAAAAAAAAUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAABABQAAACsAAAAFAABABQAAAAAAAAAKAAAAAEAAACEAAAAhAADAAAAAAAA////AQAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    // NUSPacker가 실제로 생성한 title.cert (타이틀에 무관한 고정 인증서 체인 — 그대로 복사해서 쓰면 됨)
    private const string CertTemplateBase64 =
        "AAEAAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABSb290AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAABDQTAwMDAwMDAzAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUm9vdC1DQTAwMDAwMDAzAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFDUDAwMDAwMDBiAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUm9vdC1DQTAwMDAwMDAzAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAFYUzAwMDAwMDBjAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAA==";

    /// <summary>WUP 하나를 outputFolder에 만든다. groups[0]이 최우선순위(주로 code) 콘텐츠가 되도록
    /// 순서대로 index 1..N을 부여하며, index 0(FST)은 이 메서드가 자동으로 만들어 채운다.</summary>
    public static void Pack(string outputFolder, ulong titleId, ushort titleVersion, IReadOnlyList<WupContentGroup> groups)
    {
        Directory.CreateDirectory(outputFolder);

        // 1) 타이틀키는 아무 값이나 우리가 정해도 됨 (실기 커먼키로 암호화해서 티켓에 넣으면 콘솔이 복호화 가능)
        byte[] titleKey = RandomNumberGenerator.GetBytes(16);

        // 2) 콘텐츠 배치: 각 그룹의 파일들에 (clusterIndex, offsetField, sizeField) 배정
        var contentRecords = new List<(int Index, bool Hashed, byte[] PlainBlob, List<(WupFileEntry File, uint OffsetField, uint SizeField)> Layout)>();

        int nextIndex = 1; // index 0은 FST 전용

        foreach (var group in groups)
        {
            if (group.Files.Count == 0) continue;

            var layout = new List<(WupFileEntry, uint, uint)>();
            using var blob = new MemoryStream();

            foreach (var file in group.Files)
            {
                // NUSPacker와 동일하게 offsetFactor(32)의 배수 경계에 정렬
                long pad = (32 - (blob.Length % 32)) % 32;
                if (pad > 0) blob.Write(new byte[pad]);

                uint offsetField = (uint)(blob.Length / 32);
                blob.Write(file.Data);

                layout.Add((file, offsetField, (uint)file.Data.Length));
            }

            contentRecords.Add((nextIndex, group.Hashed, blob.ToArray(), layout));
            nextIndex++;
        }

        // 3) FST 작성 (index 0 콘텐츠가 될 평문 데이터)
        byte[] fstPlain = BuildFst(contentRecords);

        // 4) 콘텐츠 암호화 (index 0 = FST, raw) + 나머지
        var finalContents = new List<(uint Cid, ushort Index, ushort Type, ulong PlainSize, byte[] EncryptedAppBytes, byte[]? H3)>();

        byte[] fstEncrypted = EncryptRawContent(fstPlain, 0, titleKey);
        finalContents.Add(((uint)0, 0, 0x2001, (ulong)fstPlain.Length, fstEncrypted, null));

        foreach (var (index, hashed, plainBlob, _) in contentRecords)
        {
            if (hashed)
            {
                var (enc, h3) = EncryptHashedContent(plainBlob, (ushort)index, titleKey);
                finalContents.Add(((uint)index, (ushort)index, 0x2003, (ulong)plainBlob.Length, enc, h3));
            }
            else
            {
                var enc = EncryptRawContent(plainBlob, (ushort)index, titleKey);
                finalContents.Add(((uint)index, (ushort)index, 0x2001, (ulong)plainBlob.Length, enc, null));
            }
        }

        // 5) 파일로 저장
        foreach (var c in finalContents)
        {
            File.WriteAllBytes(Path.Combine(outputFolder, $"{c.Cid:x8}.app"), c.EncryptedAppBytes);

            if (c.H3 is not null)
                File.WriteAllBytes(Path.Combine(outputFolder, $"{c.Cid:x8}.h3"), c.H3);
        }

        // 6) TMD 작성 (콘텐츠 해시는 암호화된 .app 바이트 기준)
        var tmdContents = finalContents
            .Select(c => (c.Cid, c.Index, c.Type, c.PlainSize, Hash: SHA256.HashData(c.EncryptedAppBytes)))
            .ToList();

        byte[] tmd = BuildTmd(titleId, titleVersion, tmdContents);
        File.WriteAllBytes(Path.Combine(outputFolder, "title.tmd"), tmd);

        // 7) 티켓 작성 (템플릿 패치)
        byte[] tik = BuildTicket(titleId, titleKey);
        File.WriteAllBytes(Path.Combine(outputFolder, "title.tik"), tik);

        // 8) 인증서 (타이틀 무관 고정 템플릿)
        File.WriteAllBytes(Path.Combine(outputFolder, "title.cert"), Convert.FromBase64String(CertTemplateBase64));
    }

    #region FST Writer

    private sealed class FstDirNode
    {
        public readonly SortedDictionary<string, FstDirNode> Dirs = new(StringComparer.Ordinal);
        public readonly List<(string Name, int ClusterIndex, uint OffsetField, uint SizeField)> Files = [];
    }

    private static byte[] BuildFst(List<(int Index, bool Hashed, byte[] PlainBlob, List<(WupFileEntry File, uint OffsetField, uint SizeField)> Layout)> contentRecords)
    {
        var root = new FstDirNode();

        foreach (var (index, _, _, layout) in contentRecords)
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
            nameTableSize += Encoding_UTF8_ByteCount(name) + 1;

            return off;
        }

        var entries = new List<(bool IsDir, string Name, int ParentOrCluster, int DirEndOrOffset, uint FileSize, ushort ClusterIndex)>
        {
            (true, "", 0, 0, 0, 0), // root, DirEndIndex는 마지막에 채움
        };

        void Serialize(FstDirNode node, int parentIndex)
        {
            foreach (var (name, child) in node.Dirs)
            {
                int dirIndex = entries.Count;

                entries.Add((true, name, parentIndex, 0, 0, 0));
                Serialize(child, parentIndex: dirIndex);

                var e = entries[dirIndex];
                entries[dirIndex] = (e.IsDir, e.Name, e.ParentOrCluster, entries.Count, e.FileSize, e.ClusterIndex);
            }

            foreach (var (name, clusterIndex, offsetField, sizeField) in node.Files)
                entries.Add((false, name, (int)offsetField, 0, sizeField, (ushort)clusterIndex));
        }

        Serialize(root, parentIndex: 0);

        entries[0] = (true, "", 0, entries.Count, 0, 0);

        // 이름 테이블 오프셋 미리 계산
        foreach (var e in entries.Skip(1))
            GetNameOffset(e.Name);

        int numCluster = contentRecords.Count + 1; // +1 = FST 자신(cluster0)
        int clusterTableOffset = 0x20;
        int fileTableOffset = clusterTableOffset + numCluster * 0x20;
        int nameTableOffset = fileTableOffset + entries.Count * 0x10;

        using var ms = new MemoryStream();
        var bw = new BeWriter(ms);

        bw.U32(0x46535400); // "FST\0"
        bw.U32(32); // offsetFactor
        bw.U32((uint)numCluster);
        ms.Write(new byte[clusterTableOffset - (int)ms.Length]);

        // cluster0 = FST 자신 (읽기 쪽에서 특별 취급, 여기서는 사용 안 함)
        bw.U32(0); bw.U32(0);
        ms.Write(new byte[0x14 - 8]);
        ms.WriteByte(0); // hashMode
        ms.Write(new byte[0x20 - 0x15]);

        foreach (var (_, hashed, _, _) in contentRecords)
        {
            bw.U32(0); bw.U32(0); // offUnits/sizeUnits: 실기에서 강제 검증 안 하는 것으로 확인됨(0으로 둬도 무방)
            ms.Write(new byte[0x14 - 8]);
            ms.WriteByte((byte)(hashed ? 2 : 1));
            ms.Write(new byte[0x20 - 0x15]);
        }

        foreach (var e in entries)
        {
            uint typeAndNameOffset = (uint)(GetNameOffset(e.Name) & 0xFFFFFF) | (e.IsDir ? 0x01000000u : 0u);

            bw.U32(typeAndNameOffset);
            bw.U32((uint)e.ParentOrCluster);
            bw.U32(e.IsDir ? (uint)e.DirEndOrOffset : e.FileSize);
            bw.U16(0); // unknown
            bw.U16(e.ClusterIndex);
        }

        foreach (var name in names)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(name);
            ms.Write(bytes);
            ms.WriteByte(0);
        }

        var result = ms.ToArray();

        // AES-CBC(PaddingMode.None)는 16바이트 배수여야 함
        int padded = (result.Length + 15) / 16 * 16;

        if (padded != result.Length)
            Array.Resize(ref result, padded);

        return result;
    }

    private static int Encoding_UTF8_ByteCount(string s) => System.Text.Encoding.UTF8.GetByteCount(s);

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

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0, 4), 0x00010004); // RSA_2048_SHA256, 서명값은 0으로 둠(실기는 시그패치로 검증 스킵)

        var issuer = "Root-CA00000003-CP0000000b\0"u8;
        issuer.CopyTo(buf.AsSpan(bodyStart, issuer.Length));

        buf[bodyStart + 0x40] = 1; // version

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(bodyStart + 0x44, 8), 0x000500101000400AUL); // systemVersion
        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(bodyStart + 0x4C, 8), titleId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(bodyStart + 0x54, 4), 0x00000100); // titleType
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x58, 2), 0); // groupId
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(bodyStart + 0x9A, 4), 0x80000000); // appType (일반 타이틀)
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x9C, 2), titleVersion);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0x9E, 2), (ushort)contents.Count);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(bodyStart + 0xA0, 2), 0); // bootIndex

        // content_chunk_records
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

        // content_info[0] = index 0, count=전체 콘텐츠 수, hash=SHA256(전체 content_chunk_records 바이트)
        byte[] contentTableBytes = buf.AsSpan(contentTableOffset, contents.Count * 48).ToArray();
        byte[] contentTableHash = SHA256.HashData(contentTableBytes);

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(contentInfoOffset, 2), 0); // index
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(contentInfoOffset + 2, 2), (ushort)contents.Count); // count
        contentTableHash.CopyTo(buf.AsSpan(contentInfoOffset + 4, 32));

        // SHA2 @bodyStart+0xA4 = content_info 테이블(64*36바이트) 전체의 해시
        byte[] contentInfoBytes = buf.AsSpan(contentInfoOffset, 64 * 36).ToArray();
        byte[] contentInfoHash = SHA256.HashData(contentInfoBytes);
        contentInfoHash.CopyTo(buf.AsSpan(bodyStart + 0xA4, 32));

        return buf;
    }

    #endregion

    #region Ticket Writer

    private static byte[] BuildTicket(ulong titleId, byte[] titleKeyPlain)
    {
        byte[] tik = Convert.FromBase64String(TicketTemplateBase64);

        byte[] iv = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(iv.AsSpan(0, 8), titleId);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = WiiUCommonKey;
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
        int padded = (plain.Length + 15) / 16 * 16;
        byte[] buf = new byte[Math.Max(padded, 16)];
        plain.CopyTo(buf, 0);

        byte[] iv = new byte[16];
        iv[0] = (byte)(contentIndex >> 8);
        iv[1] = (byte)(contentIndex & 0xFF);

        AesCbcEncryptInPlace(buf, buf.Length, titleKey, iv);

        return buf;
    }

    private static (byte[] Encrypted, byte[] H3) EncryptHashedContent(byte[] plain, ushort contentIndex, byte[] titleKey)
    {
        int chunkCount = Math.Max(1, (plain.Length + HashedDataSize - 1) / HashedDataSize);

        if (chunkCount > HashedMaxChunks)
            throw new NotSupportedException(
                $"콘텐츠가 너무 커서(해시트리 1단계 한계 {HashedMaxChunks}청크 ≈ {HashedMaxChunks * (long)HashedDataSize / 1024 / 1024}MB 초과) " +
                "단일 hashed 콘텐츠로 만들 수 없습니다. 여러 콘텐츠로 나눠서 담아야 합니다.");

        byte[] padded = new byte[chunkCount * HashedDataSize];
        plain.CopyTo(padded, 0);

        var h0Hashes = new byte[chunkCount][];

        for (int i = 0; i < chunkCount; i++)
            h0Hashes[i] = SHA1.HashData(padded.AsSpan(i * HashedDataSize, HashedDataSize));

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
                    h0Hashes[chunkIdx].CopyTo(table, j * 20);
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
            int h0Group = i / 16;
            int h1Group = h0Group / 16;

            var header = new byte[HashedHeaderSize];
            h0Tables[h0Group].CopyTo(header, 0);
            h1Tables[h1Group].CopyTo(header, 0x140);
            h2Table.CopyTo(header, 0x280);

            AesCbcEncryptInPlace(header, HashedHeaderSize, titleKey, new byte[16]);

            var chunk = padded.AsSpan(i * HashedDataSize, HashedDataSize).ToArray();
            byte[] iv = h0Hashes[i].AsSpan(0, 16).ToArray();

            AesCbcEncryptInPlace(chunk, HashedDataSize, titleKey, iv);

            outStream.Write(header, 0, HashedHeaderSize);
            outStream.Write(chunk, 0, HashedDataSize);
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