using System.Buffers.Binary;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class LayoutBuilder
{
    public const int MaximumHeaderSize = 0x1000000;

    private const int FileAlignment = 0x20;

    private readonly WiiFstTree _tree;
    private readonly long _fstOffset;
    private readonly long _fstSize;
    private readonly List<RepackedPartition.Extent> _extents = [];
    private long _cursor;

    public LayoutBuilder(WiiFstTree tree, long dolOffset, long dolSize)
    {
        _tree = tree;
        _fstSize = tree.GetSerializedSize();
        _fstOffset = Align(dolOffset + dolSize);
        _cursor = Align(_fstOffset + _fstSize);
    }

    public static long Align(long value) => (value + FileAlignment - 1) / FileAlignment * FileAlignment;

    public static long ReadOffset(byte[] boot, int position) => (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(position)) << 2;

    public static void EnsureAnyApplied(IReadOnlyList<(string Path, bool Found)> resolved)
    {
        if (resolved.Count > 0 && resolved.All(r => !r.Found))
            throw new InvalidDataException($"패치 파일이 이 디스크에 하나도 없습니다 (총 {resolved.Count}개). 다른 게임이거나 다른 버전용 패치일 수 있습니다: {string.Join(", ", resolved.Take(3).Select(r => r.Path))}");
    }

    public static void EnsureFileSize(long size, string path)
    {
        if (size > uint.MaxValue)
            throw new InvalidDataException($"파일이 너무 큽니다 (4GB 미만이어야 합니다): {path}");
    }

    public void PlaceFile(FstNode node, long size, string path)
    {
        long offset = Place(node, size);

        if (size > 0)
            _extents.Add(RepackedPartition.Extent.FromFile(offset, size, path));
    }

    public void PlaceFile(FstNode node, long size, PartitionReader original, long originalOffset)
    {
        long offset = Place(node, size);

        if (size > 0)
            _extents.Add(RepackedPartition.Extent.FromReader(offset, size, original, originalOffset));
    }

    public RepackPlan Build(byte[] head, long fstMaxSize, RepackedPartition.Extent dol, IReadOnlyList<PatchEntry> entries)
    {
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x424), (uint)(_fstOffset >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x428), (uint)(_fstSize >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x42C), (uint)(Math.Max(fstMaxSize, _fstSize) >> 2));
        _extents.Add(RepackedPartition.Extent.FromMemory(0, head));
        _extents.Add(dol);
        _extents.Add(RepackedPartition.Extent.FromMemory(_fstOffset, _tree.Serialize()));

        long clusters = (_cursor + Layout.BlockDataSize - 1) / Layout.BlockDataSize;

        return new RepackPlan(new RepackedPartition(_extents, clusters * Layout.BlockDataSize), clusters * Layout.BlockTotalSize, entries);
    }

    private long Place(FstNode node, long size)
    {
        long offset = _cursor;

        node.Offset = offset;
        node.Size = size;

        _cursor = Align(offset + size);

        return offset;
    }
}