using System.Buffers.Binary;
using WiiGC.Core.Interfaces;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class FolderBaseSource : IRvzInputSource
{
    private const int PartitionEntriesOffset = Layout.PartitionTableOffset + Layout.PartitionTableSize;
    private const int RegionOffset = 0x4E000;

    private readonly byte[] _head;
    private readonly byte[] _partition;
    private readonly byte[] _tmd;
    private readonly byte[] _cert;
    private readonly long _tmdOffset;
    private readonly long _certOffset;

    private FolderBaseSource(byte[] head, byte[] partition, byte[] tmd, byte[] cert, long containerOffset, long dataOffset, long length)
    {
        _head = head;
        _partition = partition;
        _tmd = tmd;
        _cert = cert;
        ContainerOffset = containerOffset;
        DataOffset = dataOffset;
        Length = length;
        _tmdOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.TmdOffsetField)) << 2);
        _certOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.CertOffsetField)) << 2);
    }

    public long Length { get; }

    public long ContainerOffset { get; }

    public long DataOffset { get; }

    public static FolderBaseSource Create(string folder, FolderInfo info, long dataSize)
    {
        string meta = Path.Combine(folder, FolderLayout.Meta);
        string discPath = Path.Combine(meta, FolderLayout.Disc);
        string partitionPath = Path.Combine(meta, FolderLayout.Partition);
        string tmdPath = Path.Combine(meta, FolderLayout.Tmd);
        string certPath = Path.Combine(meta, FolderLayout.Cert);

        if (!File.Exists(discPath) || !File.Exists(partitionPath) || !File.Exists(tmdPath) || !File.Exists(certPath))
            throw new InvalidDataException("언팩 폴더의 meta 파일이 없습니다. 이 버전에서 다시 언팩해 주세요.");

        byte[] head = File.ReadAllBytes(discPath);
        byte[] partition = File.ReadAllBytes(partitionPath);
        byte[] tmd = File.ReadAllBytes(tmdPath);
        byte[] cert = File.ReadAllBytes(certPath);

        if (head.Length != FolderLayout.DiscHeadSize || partition.Length != FolderLayout.PartitionHeaderSize)
            throw new InvalidDataException("meta 파일 크기가 올바르지 않습니다.");

        int tmdSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.TmdSizeField));
        int certSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.CertSizeField));

        if (tmd.Length != tmdSize || cert.Length != certSize || tmdSize < PartitionHeader.TmdMinimumSize)
            throw new InvalidDataException("meta의 TMD/인증서 크기가 파티션 헤더와 맞지 않습니다.");

        long dataOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.DataOffsetField)) << 2;
        long h3Offset = (long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(PartitionHeader.H3OffsetField)) << 2;

        if (h3Offset + PartitionHeader.H3TableSize > dataOffset)
            throw new InvalidDataException("meta의 파티션 헤더 위치가 올바르지 않습니다.");

        Array.Clear(head, Layout.PartitionTableOffset, RegionOffset - Layout.PartitionTableOffset);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(Layout.PartitionTableOffset), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(Layout.PartitionTableOffset + 4), PartitionEntriesOffset >> 2);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionEntriesOffset), (uint)(info.ContainerOffset >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionEntriesOffset + 4), 0);

        long end = info.ContainerOffset + dataOffset + dataSize;
        long length = Math.Max(info.DiscLength, (end + 0x7FFF) / 0x8000 * 0x8000);

        return new FolderBaseSource(head, partition, tmd, cert, info.ContainerOffset, dataOffset, length);
    }

    public PartitionSpec CreateSpec(long dataSize) => new(ContainerOffset, ContainerOffset + DataOffset, dataSize, Ticket.DecryptTitleKey(_partition));

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset + destination.Length > Length)
            throw new EndOfStreamException("디스크 범위를 벗어난 읽기입니다.");

        destination.Clear();
        Overlay(offset, destination, 0, _head);
        Overlay(offset, destination, ContainerOffset, _partition);
        Overlay(offset, destination, _tmdOffset, _tmd);
        Overlay(offset, destination, _certOffset, _cert);
    }

    public void Dispose() { }

    private static void Overlay(long offset, Span<byte> destination, long blockOffset, byte[] block)
    {
        long from = Math.Max(offset, blockOffset);
        long to = Math.Min(offset + destination.Length, blockOffset + block.Length);

        if (to > from)
            block.AsSpan((int)(from - blockOffset), (int)(to - from)).CopyTo(destination[(int)(from - offset)..]);
    }
}