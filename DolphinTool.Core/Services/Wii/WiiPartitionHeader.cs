using System.Buffers.Binary;

namespace DolphinTool.Core.Services.Wii;

internal sealed record WiiPartitionHeader(long TmdOffset, int TmdSize, long H3Offset)
{
    public const int H3TableSize = 0x18000;

    public const int TmdContentHashOffset = 0x1F4;

    public const int TmdMinimumSize = TmdContentHashOffset + 20;

    public static WiiPartitionHeader Read(IRvzInputSource input, long containerOffset)
    {
        Span<byte> buffer = stackalloc byte[20];

        input.Read(containerOffset + 0x2A4, buffer);

        int tmdSize = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        long tmdOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]) << 2);
        long h3Offset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(buffer[16..]) << 2);

        if (tmdSize < TmdMinimumSize || tmdOffset + tmdSize > input.Length || h3Offset + H3TableSize > input.Length)
            throw new InvalidDataException("Wii 파티션 헤더(TMD/H3) 위치가 올바르지 않습니다.");

        return new WiiPartitionHeader(tmdOffset, tmdSize, h3Offset);
    }
}