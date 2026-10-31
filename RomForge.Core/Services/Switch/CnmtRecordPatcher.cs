using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class CnmtRecordPatcher
{
    private const int HeaderSize = 0x20;
    private const int ExtendedHeaderSizeOffset = 0xE;
    private const int ContentCountOffset = 0x10;
    private const int RecordSize = 0x38;
    private const int HashOffset = 0x0;
    private const int NcaIdOffset = 0x20;
    private const int SizeOffset = 0x30;
    private const int TypeOffset = 0x36;
    private const int IdOffsetOffset = 0x37;

    public const byte ProgramType = 0x1;

    public const byte DataType = 0x2;

    public readonly record struct ContentRecord(byte[] Hash, byte[] NcaId, long Size, byte Type, byte IdOffset);

    public static List<ContentRecord> ReadRecords(byte[] cnmt)
    {
        int start = GetRecordsStart(cnmt, out int count);
        var records = new List<ContentRecord>(count);

        for (int i = 0; i < count; i++)
        {
            var record = cnmt.AsSpan(start + i * RecordSize, RecordSize);

            records.Add(new ContentRecord(record[..0x20].ToArray(), record.Slice(NcaIdOffset, 0x10).ToArray(), ReadSize(record.Slice(SizeOffset, 6)), record[TypeOffset], record[IdOffsetOffset]));
        }

        return records;
    }

    public static byte[] Replace(byte[] cnmt, byte[] oldNcaId, byte[] newNcaId, byte[] newHash, long newSize)
    {
        if (newNcaId.Length != 0x10 || newHash.Length != 0x20)
            throw new ArgumentException("NcaId는 16바이트, Hash는 32바이트여야 합니다.");

        if (newSize < 0 || newSize > 0xFFFFFFFFFFFFL)
            throw new ArgumentOutOfRangeException(nameof(newSize));

        int start = GetRecordsStart(cnmt, out int count);
        var result = (byte[])cnmt.Clone();

        for (int i = 0; i < count; i++)
        {
            int offset = start + i * RecordSize;

            if (!result.AsSpan(offset + NcaIdOffset, 0x10).SequenceEqual(oldNcaId))
                continue;

            newHash.CopyTo(result, offset + HashOffset);
            newNcaId.CopyTo(result, offset + NcaIdOffset);

            for (int b = 0; b < 6; b++)
                result[offset + SizeOffset + b] = (byte)(newSize >> (8 * b));

            return result;
        }

        throw new InvalidOperationException("cnmt에서 대상 Content 레코드를 찾을 수 없습니다.");
    }

    public static byte[] Add(byte[] cnmt, ContentRecord record)
    {
        if (record.NcaId.Length != 0x10 || record.Hash.Length != 0x20)
            throw new ArgumentException("NcaId는 16바이트, Hash는 32바이트여야 합니다.");

        if (record.Size < 0 || record.Size > 0xFFFFFFFFFFFFL)
            throw new ArgumentOutOfRangeException(nameof(record));

        int start = GetRecordsStart(cnmt, out int count);

        if (count >= ushort.MaxValue)
            throw new InvalidOperationException("cnmt Content 레코드 수가 한계를 넘었습니다.");

        int insertIndex = count;

        for (int i = 0; i < count; i++)
        {
            if (cnmt[start + i * RecordSize + TypeOffset] > record.Type)
            {
                insertIndex = i;
                break;
            }
        }

        int offset = start + insertIndex * RecordSize;
        var result = new byte[cnmt.Length + RecordSize];

        Array.Copy(cnmt, 0, result, 0, offset);
        Array.Copy(cnmt, offset, result, offset + RecordSize, cnmt.Length - offset);
        record.Hash.CopyTo(result, offset + HashOffset);
        record.NcaId.CopyTo(result, offset + NcaIdOffset);

        for (int b = 0; b < 6; b++)
            result[offset + SizeOffset + b] = (byte)(record.Size >> (8 * b));

        result[offset + TypeOffset] = record.Type;
        result[offset + IdOffsetOffset] = record.IdOffset;

        BitConverter.GetBytes((ushort)(count + 1)).CopyTo(result, ContentCountOffset);

        return result;
    }

    private static int GetRecordsStart(byte[] cnmt, out int count)
    {
        if (cnmt.Length < HeaderSize)
            throw new InvalidDataException("cnmt 크기가 올바르지 않습니다.");

        int extendedHeaderSize = BitConverter.ToUInt16(cnmt, ExtendedHeaderSizeOffset);

        count = BitConverter.ToUInt16(cnmt, ContentCountOffset);

        int start = HeaderSize + extendedHeaderSize;

        if (start + count * RecordSize > cnmt.Length)
            throw new InvalidDataException("cnmt Content 레코드 영역이 파일 크기를 벗어납니다.");

        return start;
    }

    private static long ReadSize(ReadOnlySpan<byte> bytes)
    {
        long size = 0;

        for (int i = 0; i < 6; i++)
            size |= (long)bytes[i] << (8 * i);

        return size;
    }
}