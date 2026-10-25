using System.Buffers.Binary;
using System.Text;

namespace N3DS.Core.Models;

public class ExeFsHeader
{
    public const int Size = 0x200;
    public const int MaxEntries = 8;

    public ExeFsEntry[] Entries { get; private set; } = new ExeFsEntry[MaxEntries];

    public byte[][] Hashes { get; private set; } = new byte[MaxEntries][];

    public static ExeFsHeader Parse(byte[] data, int offset = 0)
    {
        var h = new ExeFsHeader();

        for (int i = 0; i < MaxEntries; i++)
        {
            int b = offset + i * 0x10;

            h.Entries[i] = new ExeFsEntry
            {
                Name = Encoding.ASCII.GetString(data, b, 8).TrimEnd('\0'),
                Offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(b + 8)),
                Size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(b + 12)),
            };
        }

        for (int i = 0; i < MaxEntries; i++)
        {
            int b = offset + 0x100 + i * 0x20;

            h.Hashes[i] = data.AsSpan(b, 0x20).ToArray();
        }

        return h;
    }

    public byte[] GetHashForEntry(int entryIndex) => Hashes[MaxEntries - 1 - entryIndex];
}