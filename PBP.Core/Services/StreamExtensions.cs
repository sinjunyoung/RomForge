using PBP.Core.Models;

namespace PBP.Core.Services;

public static class StreamExtensions
{
    public static void Read(this Stream stream, uint[] buffer, int count)
    {
        var uintBuffer = new byte[sizeof(uint)];

        for (var i = 0; i < count; i++)
        {
            stream.Read(uintBuffer, 0, 4);
            buffer[i] = BitConverter.ToUInt32(uintBuffer, 0);
        }
    }

    public static void Read(this Stream stream, int[] buffer, int count)
    {
        var intBuffer = new byte[sizeof(int)];
        for (var i = 0; i < count; i++)
        {
            stream.Read(intBuffer, 0, 4);
            buffer[i] = BitConverter.ToInt32(intBuffer, 0);
        }
    }

    public static ushort ReadUInt16(this Stream stream)
    {
        var buf = new byte[2];
        stream.Read(buf, 0, 2);

        return BitConverter.ToUInt16(buf, 0);
    }

    public static int ReadInteger(this Stream stream)
    {
        var buf = new byte[4];
        stream.Read(buf, 0, 4);

        return BitConverter.ToInt32(buf, 0);
    }

    public static uint ReadUInteger(this Stream stream)
    {
        var buf = new byte[4];
        stream.Read(buf, 0, 4);

        return BitConverter.ToUInt32(buf, 0);    
    }

    public static SfoFile ReadSFO(this Stream stream, long sfoOffset)
    {
        var sfo = new SfoFile
        {
            Magic = stream.ReadUInteger(),
            Version = stream.ReadUInteger(),
            KeyTableOffset = stream.ReadUInteger(),
            DataTableOffset = stream.ReadUInteger()
        };

        var entryCount = stream.ReadInteger();

        for (var i = 0; i < entryCount; i++)
        {
            sfo.Entries.Add(new SfoIndexEntry
            {
                KeyOffset = stream.ReadUInt16(),
                Format = stream.ReadUInt16(),
                Length = stream.ReadUInteger(),
                MaxLength = stream.ReadUInteger(),
                DataOffset = stream.ReadUInteger(),
            });
        }

        var keyBase = sfoOffset + sfo.KeyTableOffset;

        foreach (var entry in sfo.Entries)
        {
            stream.Seek(keyBase + entry.KeyOffset, SeekOrigin.Begin);
            entry.Key = stream.ReadNullTerminatedString();
        }

        var dataBase = sfoOffset + sfo.DataTableOffset;

        foreach (var entry in sfo.Entries)
        {
            stream.Seek(dataBase + entry.DataOffset, SeekOrigin.Begin);
            entry.Value = entry.Format switch
            {
                0x0204 => stream.ReadNullTerminatedString(),
                0x0404 => stream.ReadInteger(),
                _ => null
            };
        }

        return sfo;
    }

    public static string ReadNullTerminatedString(this Stream stream)
    {
        var bytes = new List<byte>();
        int b;

        while ((b = stream.ReadByte()) > 0)
            bytes.Add((byte)b);

        return System.Text.Encoding.ASCII.GetString([.. bytes]);
    }

    public static void Write(this Stream stream, uint[] buffer, int count, int size)
    {
        var p = 0;
        var i = 0;

        while (p < size)
        {
            stream.Write(BitConverter.GetBytes(buffer[i]), 0, sizeof(uint));
            p += sizeof(uint);
            i++;
        }
    }

    public static void Write(this Stream stream, IsoIndexHeader[] isoIndices, int offset, int size)
    {
        foreach (var index in isoIndices)
        {
            stream.Write(BitConverter.GetBytes(index.Offset), 0, sizeof(uint));
            stream.Write(BitConverter.GetBytes(index.Length), 0, sizeof(uint));

            for (var j = 0; j < index.Dummy.Length; j++)
                stream.Write(BitConverter.GetBytes(index.Dummy[j]), 0, sizeof(uint));
        }
    }

    public static void Write(this Stream stream, string buffer, int offset, int size)
    {
        var membuf = System.Text.Encoding.ASCII.GetBytes(buffer);

        stream.Write(membuf, offset, size);
    }

    public static void Write(this Stream stream, char value) => stream.WriteByte((byte)value);

    public static void WriteUInt16(this Stream stream, ushort value, int count)
    {
        for (var p = 0; p < count; p++)
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(ushort));
    }

    public static void WriteUInt32(this Stream stream, uint value, int count)
    {
        for (var p = 0; p < count; p++)
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(uint));
    }

    public static void WriteInt32(this Stream stream, int value, int count)
    {
        for (var p = 0; p < count; p++)
            stream.Write(BitConverter.GetBytes(value), 0, sizeof(int));
    }

    public static void WriteChar(this Stream stream, byte value, int count)
    {
        for (var p = 0; p < count; p++)
            stream.WriteByte(value);
    }

    public static void WriteRandom(this Stream stream, int count)
    {
        var buffer = new uint[count];
        var r = new Random();

        for (var i = 0; i < count; i++)
            buffer[i] = (uint)r.Next(0, 0xFFFF);

        stream.Write(buffer, 0, count * sizeof(uint));
    }

    public static void WriteResource(this Stream stream, byte[]? resource)
    {
        if (resource is null || resource.Length == 0) 
            return;

        stream.Write(resource, 0, resource.Length);
    }

    public static void WriteSFO(this Stream stream, SfoFile sfo)
    {
        stream.WriteUInt32(sfo.Magic, 1);
        stream.WriteUInt32(sfo.Version, 1);
        stream.WriteUInt32(sfo.KeyTableOffset, 1);
        stream.WriteUInt32(sfo.DataTableOffset, 1);
        stream.WriteInt32((ushort)sfo.Entries.Count, 1);

        foreach (var entry in sfo.Entries)
        {
            stream.WriteUInt16(entry.KeyOffset, 1);
            stream.WriteUInt16(entry.Format, 1);
            stream.WriteUInt32(entry.Length, 1);
            stream.WriteUInt32(entry.MaxLength, 1);
            stream.WriteUInt32(entry.DataOffset, 1);
        }

        foreach (var entry in sfo.Entries)
        {
            stream.Write(entry.Key, 0, entry.Key.Length);
            stream.WriteByte(0);
        }

        for (var i = 0; i < sfo.Padding; i++) 
            stream.WriteByte(0);

        foreach (var entry in sfo.Entries)
        {
            switch (entry.Format)
            {
                case 0x0204:
                    var s = (string)entry.Value!;
                    stream.Write(s, 0, s.Length);
                    stream.WriteByte(0);
                    for (var j = 0; j < entry.MaxLength - entry.Length; j++) 
                        stream.WriteByte(0);
                    break;
                case 0x0404:
                    stream.WriteInt32(Convert.ToInt32(entry.Value), 1);
                    break;
            }
        }
    }
}