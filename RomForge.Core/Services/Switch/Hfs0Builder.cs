using System.Buffers.Binary;

namespace RomForge.Core.Services.Switch;

public class Hfs0Builder
{
    private const uint MagicHfs0 = 0x30534648;
    private const int EntrySize = 0x40;
    private const int HeaderBaseSize = 0x10;

    private readonly List<Hfs0BuilderEntry> _files = [];

    private record Hfs0BuilderEntry(string Name, ulong Offset, ulong Size, byte[] Hash, uint HashTargetSize);

    public void AddFile(string name, ulong size, byte[] hash, uint hashTargetSize)
    {
        ulong offset = _files.Count == 0 ? 0 : _files[^1].Offset + _files[^1].Size;

        _files.Add(new Hfs0BuilderEntry(name, offset, size, hash, hashTargetSize));
    }

    public void AddFileWithOffset(string name, ulong offset, ulong size, byte[] hash, uint hashTargetSize) => _files.Add(new Hfs0BuilderEntry(name, offset, size, hash, hashTargetSize));

    public ulong RawHeaderSize()
    {
        ulong stringTableSize = (ulong)_files.Sum(f => f.Name.Length + 1);
        return (ulong)(HeaderBaseSize + EntrySize * _files.Count) + stringTableSize;
    }

    public ulong AlignedHeaderSize(ulong alignment)
    {
        ulong raw = RawHeaderSize();
        return alignment > 1 ? AlignUp(raw, alignment) : raw;
    }

    public byte[] BuildHeader(ulong alignment = 1)
    {
        int stringTableRawSize = _files.Sum(f => f.Name.Length + 1);
        ulong rawSize = (ulong)(HeaderBaseSize + EntrySize * _files.Count) + (ulong)stringTableRawSize;
        ulong totalSize = alignment > 1 ? AlignUp(rawSize, alignment) : rawSize;
        int padding = (int)(totalSize - rawSize);
        byte[] header = new byte[totalSize];
        var span = header.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span, MagicHfs0);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)_files.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)(stringTableRawSize + padding));

        int entryBase = HeaderBaseSize;
        int stringTableBase = HeaderBaseSize + EntrySize * _files.Count;
        int stringOffset = 0;

        for (int i = 0; i < _files.Count; i++)
        {
            var f = _files[i];
            var entrySpan = span[(entryBase + i * EntrySize)..];

            BinaryPrimitives.WriteUInt64LittleEndian(entrySpan, f.Offset);
            BinaryPrimitives.WriteUInt64LittleEndian(entrySpan[8..], f.Size);
            BinaryPrimitives.WriteUInt32LittleEndian(entrySpan[16..], (uint)stringOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(entrySpan[20..], f.HashTargetSize);
            f.Hash.CopyTo(entrySpan[32..]);

            var nameBytes = System.Text.Encoding.UTF8.GetBytes(f.Name);

            nameBytes.CopyTo(header, stringTableBase + stringOffset);

            stringOffset += nameBytes.Length + 1;
        }

        return header;
    }

    private static ulong AlignUp(ulong value, ulong alignment) => (value + alignment - 1) & ~(alignment - 1);
}