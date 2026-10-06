using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiPartitionPlanner
{
    private const int FileAlignment = 0x20;
    private const int BootSize = 0x440;
    private const int MinimumHeaderSize = 0x2440;
    private const int MaximumHeaderSize = 0x1000000;
    private const int MaximumFstSize = 0x4000000;

    public static WiiRepackPlan Plan(WiiPartitionReader original, IReadOnlyDictionary<string, string> replacements)
    {
        byte[] boot = new byte[BootSize];

        original.Read(0, boot);

        long dolOffset = ReadOffset(boot, 0x420);
        long fstOffset = ReadOffset(boot, 0x424);
        long fstSize = ReadOffset(boot, 0x428);
        long fstMaxSize = ReadOffset(boot, 0x42C);

        if (dolOffset < MinimumHeaderSize || dolOffset > MaximumHeaderSize || fstSize < 12 || fstSize > MaximumFstSize || fstOffset + fstSize > original.Length)
            throw new InvalidDataException("Wii 파티션 부트 정보가 올바르지 않습니다.");

        byte[] dolHeader = new byte[WiiDol.HeaderSize];

        original.Read(dolOffset, dolHeader);

        long dolSize = WiiDol.GetSize(dolHeader);

        if (dolOffset + dolSize > original.Length)
            throw new InvalidDataException("DOL 크기가 올바르지 않습니다.");

        byte[] fstBytes = new byte[fstSize];

        original.Read(fstOffset, fstBytes);

        var tree = WiiFstTree.Parse(fstBytes);
        var files = tree.EnumerateFiles().ToList();
        var byPath = new Dictionary<string, WiiFstNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, node) in files)
            byPath.TryAdd(path, node);

        var external = new Dictionary<WiiFstNode, (string Path, long Size)>();
        var resolved = new List<(string Path, WiiFstNode? Node)>(replacements.Count);

        foreach (var (discPath, filePath) in replacements)
        {
            bool found = byPath.TryGetValue(discPath, out var node);

            if (found)
                external[node!] = (filePath, new FileInfo(filePath).Length);

            resolved.Add((discPath, found ? node : null));
        }

        if (resolved.Count > 0 && resolved.All(r => r.Node == null))
            throw new InvalidDataException($"패치 파일이 이 디스크에 하나도 없습니다 (총 {resolved.Count}개). 다른 게임이거나 다른 버전용 패치일 수 있습니다: {string.Join(", ", resolved.Take(3).Select(r => r.Path))}");

        byte[] head = new byte[dolOffset];

        original.Read(0, head);

        var ordered = files.Select(f => f.Node).OrderBy(n => n.Offset).ToList();
        var oldPlacement = ordered.Select(n => (n.Offset, n.Size)).ToList();
        long newFstSize = tree.GetSerializedSize();
        long newFstOffset = Align(dolOffset + dolSize);
        long cursor = Align(newFstOffset + newFstSize);
        var extents = new List<WiiRepackedPartition.Extent>();

        for (int i = 0; i < ordered.Count; i++)
        {
            var node = ordered[i];
            var (oldOffset, oldSize) = oldPlacement[i];
            long size = oldSize;

            if (external.TryGetValue(node, out var replacement))
            {
                size = replacement.Size;

                if (size > 0)
                    extents.Add(WiiRepackedPartition.Extent.FromFile(cursor, size, replacement.Path));
            }
            else if (size > 0)
                extents.Add(WiiRepackedPartition.Extent.FromReader(cursor, size, original, oldOffset));

            node.Offset = cursor;
            node.Size = size;
            cursor = Align(cursor + size);
        }

        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x424), (uint)(newFstOffset >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x428), (uint)(newFstSize >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x42C), (uint)(Math.Max(fstMaxSize, newFstSize) >> 2));
        extents.Add(WiiRepackedPartition.Extent.FromMemory(0, head));
        extents.Add(WiiRepackedPartition.Extent.FromReader(dolOffset, dolSize, original, dolOffset));
        extents.Add(WiiRepackedPartition.Extent.FromMemory(newFstOffset, tree.Serialize()));

        long clusters = (cursor + WiiLayout.BlockDataSize - 1) / WiiLayout.BlockDataSize;
        var entries = resolved.Select(r => new WiiPatchEntry(r.Path, r.Node != null, r.Node?.Offset ?? 0)).ToList();

        return new WiiRepackPlan(new WiiRepackedPartition(extents, clusters * WiiLayout.BlockDataSize), clusters * WiiLayout.BlockTotalSize, entries);
    }

    private static long ReadOffset(byte[] boot, int position) => (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(position)) << 2;

    private static long Align(long value) => (value + FileAlignment - 1) / FileAlignment * FileAlignment;
}