using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiFolderBaseSource : IRvzInputSource
{
    private const int PartitionTableOffset = 0x40000;
    private const int PartitionEntriesOffset = 0x40020;
    private const int RegionOffset = 0x4E000;

    private readonly byte[] _head;
    private readonly byte[] _partition;
    private readonly byte[] _tmd;
    private readonly byte[] _cert;
    private readonly long _tmdOffset;
    private readonly long _certOffset;

    private WiiFolderBaseSource(byte[] head, byte[] partition, byte[] tmd, byte[] cert, long containerOffset, long dataOffset, long length)
    {
        _head = head;
        _partition = partition;
        _tmd = tmd;
        _cert = cert;
        ContainerOffset = containerOffset;
        DataOffset = dataOffset;
        Length = length;
        _tmdOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2A8)) << 2);
        _certOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2B0)) << 2);
    }

    public long Length { get; }

    public long ContainerOffset { get; }

    public long DataOffset { get; }

    public static WiiFolderBaseSource Create(string folder, WiiFolderInfo info, long dataSize)
    {
        string meta = Path.Combine(folder, WiiFolderLayout.Meta);
        string discPath = Path.Combine(meta, WiiFolderLayout.Disc);
        string partitionPath = Path.Combine(meta, WiiFolderLayout.Partition);
        string tmdPath = Path.Combine(meta, WiiFolderLayout.Tmd);
        string certPath = Path.Combine(meta, WiiFolderLayout.Cert);

        if (!File.Exists(discPath) || !File.Exists(partitionPath) || !File.Exists(tmdPath) || !File.Exists(certPath))
            throw new InvalidDataException("언팩 폴더의 meta 파일이 없습니다. 이 버전에서 다시 언팩해 주세요.");

        byte[] head = File.ReadAllBytes(discPath);
        byte[] partition = File.ReadAllBytes(partitionPath);
        byte[] tmd = File.ReadAllBytes(tmdPath);
        byte[] cert = File.ReadAllBytes(certPath);

        if (head.Length != WiiFolderLayout.DiscHeadSize || partition.Length != WiiFolderLayout.PartitionHeaderSize)
            throw new InvalidDataException("meta 파일 크기가 올바르지 않습니다.");

        int tmdSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2A4));
        int certSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2AC));

        if (tmd.Length != tmdSize || cert.Length != certSize || tmdSize < WiiPartitionHeader.TmdMinimumSize)
            throw new InvalidDataException("meta의 TMD/인증서 크기가 파티션 헤더와 맞지 않습니다.");

        long dataOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2B8)) << 2;
        long h3Offset = (long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2B4)) << 2;

        if (h3Offset + WiiPartitionHeader.H3TableSize > dataOffset)
            throw new InvalidDataException("meta의 파티션 헤더 위치가 올바르지 않습니다.");

        Array.Clear(head, PartitionTableOffset, RegionOffset - PartitionTableOffset);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionTableOffset), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionTableOffset + 4), PartitionEntriesOffset >> 2);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionEntriesOffset), (uint)(info.ContainerOffset >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(PartitionEntriesOffset + 4), 0);

        long end = info.ContainerOffset + dataOffset + dataSize;
        long length = Math.Max(info.DiscLength, (end + 0x7FFF) / 0x8000 * 0x8000);

        return new WiiFolderBaseSource(head, partition, tmd, cert, info.ContainerOffset, dataOffset, length);
    }

    public WiiPartitionSpec CreateSpec(long dataSize) => new(ContainerOffset, ContainerOffset + DataOffset, dataSize, WiiTicket.DecryptTitleKey(_partition));

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