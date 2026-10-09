using System.Buffers.Binary;

namespace WiiGC.Core.Services.Wii;

internal sealed record WiiPartitionHeader(long TmdOffset, int TmdSize, long H3Offset)
{
    public const int TmdSizeField = 0x2A4;

    public const int TmdOffsetField = 0x2A8;

    public const int CertSizeField = 0x2AC;

    public const int CertOffsetField = 0x2B0;

    public const int H3OffsetField = 0x2B4;

    public const int DataOffsetField = 0x2B8;

    public const int DataSizeField = 0x2BC;

    public const int H3TableSize = 0x18000;

    public const int TmdContentHashOffset = 0x1F4;

    public const int TmdMinimumSize = TmdContentHashOffset + 20;

    public static WiiPartitionHeader Read(IRvzInputSource input, long containerOffset)
    {
        Span<byte> buffer = stackalloc byte[H3OffsetField - TmdSizeField + 4];

        input.Read(containerOffset + TmdSizeField, buffer);

        int tmdSize = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
        long tmdOffset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(buffer[(TmdOffsetField - TmdSizeField)..]) << 2);
        long h3Offset = containerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(buffer[(H3OffsetField - TmdSizeField)..]) << 2);

        if (tmdSize < TmdMinimumSize || tmdOffset + tmdSize > input.Length || h3Offset + H3TableSize > input.Length)
            throw new InvalidDataException("Wii 파티션 헤더(TMD/H3) 위치가 올바르지 않습니다.");

        return new WiiPartitionHeader(tmdOffset, tmdSize, h3Offset);
    }
}