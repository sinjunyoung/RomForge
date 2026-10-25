using N3DS.Core.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace N3DS.Core.Services;

public static class Z3dsFormat
{
    public static readonly byte[] MagicZ3DS = "Z3DS"u8.ToArray();

    public const uint SeekableMagicNumber = 0x184D2A5E;

    public const uint SeekTableFooterMagic = 0x8F92EAB1;

    public const byte FormatVersion = 0x01;

    public const byte MetaVersion = 0x01;

    public const byte MetaTypeEnd = 0x00;

    public const byte MetaTypeBinary = 0x01;

    public const int MaxMetaDataLength = 0xFFFF;

    public static int AlignUp(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    public static void WriteZ3dsHeader(Stream output, byte[] underlyingMagic, uint metadataSize, long compressedSize, long uncompressedSize)
    {
        Span<byte> header = stackalloc byte[0x20];

        header.Clear();
        MagicZ3DS.CopyTo(header[0x00..]);
        underlyingMagic.AsSpan(0, 4).CopyTo(header[0x04..]);

        header[0x08] = FormatVersion;
        header[0x09] = 0x00;

        BinaryPrimitives.WriteUInt16LittleEndian(header[0x0A..], 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(header[0x0C..], metadataSize);
        BinaryPrimitives.WriteInt64LittleEndian(header[0x10..], compressedSize);
        BinaryPrimitives.WriteInt64LittleEndian(header[0x18..], uncompressedSize);
        output.Write(header);
    }

    public static Z3dsHeader ParseZ3dsHeader(Stream input)
    {
        Span<byte> buf = stackalloc byte[0x20];

        input.ReadExactly(buf);

        if (!buf[0x00..0x04].SequenceEqual(MagicZ3DS))
            throw new InvalidDataException("Not a Z3DS file (magic mismatch)");

        return new Z3dsHeader
        {
            UnderlyingMagic = buf[0x04..0x08].ToArray(),
            Version = buf[0x08],
            HeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(buf[0x0A..]),
            MetadataSize = BinaryPrimitives.ReadUInt32LittleEndian(buf[0x0C..]),
            CompressedSize = BinaryPrimitives.ReadInt64LittleEndian(buf[0x10..]),
            UncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(buf[0x18..])
        };
    }

    public static byte[] BuildMetadata(int frameSize)
    {
        using var ms = new MemoryStream();

        ms.WriteByte(MetaVersion);
        WriteMetaItem(ms, "compressor", Encoding.UTF8.GetBytes("RomZip"));
        WriteMetaItem(ms, "date", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));

        byte[] frameSizeBytes = BitConverter.GetBytes(frameSize);

        WriteMetaItem(ms, "maxframesize", frameSizeBytes);
        ms.Write([MetaTypeEnd, 0x00, 0x00, 0x00]);

        return ms.ToArray();
    }

    private static void WriteMetaItem(Stream output, string name, byte[] data)
    {
        if (data.Length > MaxMetaDataLength)
            throw new ArgumentOutOfRangeException(nameof(data), $"metadata 값 크기가 최대치를 초과합니다: {data.Length} bytes");

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);

        output.WriteByte(MetaTypeBinary);
        output.WriteByte((byte)nameBytes.Length);
        output.WriteByte((byte)(data.Length & 0xFF));
        output.WriteByte((byte)(data.Length >> 8));
        output.Write(nameBytes);
        output.Write(data);
    }

    public static void WriteSeekTable(Stream output, List<SeekEntry> entries)
    {
        Span<byte> buf = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buf, SeekableMagicNumber);
        output.Write(buf);

        uint tableSize = (uint)(entries.Count * 8 + 9);

        BinaryPrimitives.WriteUInt32LittleEndian(buf, tableSize);
        output.Write(buf);

        foreach (var entry in entries)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, entry.CompressedSize);
            output.Write(buf);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, entry.DecompressedSize);
            output.Write(buf);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)entries.Count);
        output.Write(buf);
        output.WriteByte(0x00);
        BinaryPrimitives.WriteUInt32LittleEndian(buf, SeekTableFooterMagic);
        output.Write(buf);
    }

    public static List<SeekEntry> ParseSeekTable(Stream input, long dataStart, long compressedDataLength)
    {
        long seekTableEnd = dataStart + compressedDataLength;

        input.Position = seekTableEnd - 9;

        Span<byte> footer = stackalloc byte[9];

        input.ReadExactly(footer);

        uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(footer[0..4]);
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(footer[5..9]);

        if (magic != SeekTableFooterMagic)
            throw new InvalidDataException("Seekable zstd seek table magic mismatch");

        long tableBodyStart = seekTableEnd - 9 - (entryCount * 8);

        input.Position = tableBodyStart;

        var entries = new List<SeekEntry>((int)entryCount);
        byte[] entryPool = ArrayPool<byte>.Shared.Rent((int)entryCount * 8);

        try
        {
            input.ReadExactly(entryPool, 0, (int)entryCount * 8);

            for (int i = 0; i < entryCount; i++)
            {
                int offset = i * 8;

                entries.Add(new SeekEntry
                {
                    CompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(entryPool.AsSpan(offset, 4)),
                    DecompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(entryPool.AsSpan(offset + 4, 4))
                });
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(entryPool);
        }

        return entries;
    }
}