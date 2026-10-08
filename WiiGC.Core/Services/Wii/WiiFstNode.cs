using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiFstTree
{
    private const int MaximumDepth = 256;
    private const int MaximumNameOffset = 0xFFFFFF;

    public WiiFstNode Root { get; } = new([], true);

    public static WiiFstTree Parse(byte[] fst)
    {
        if (fst.Length < 12)
            throw new InvalidDataException("FST 크기가 올바르지 않습니다.");

        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8));

        if (entryCount < 1 || (long)entryCount * 12 > fst.Length)
            throw new InvalidDataException("FST 엔트리 수가 올바르지 않습니다.");

        var tree = new WiiFstTree();
        int index = 1;

        ParseChildren(fst, (long)entryCount * 12, ref index, (int)entryCount, tree.Root, 0);

        return tree;
    }

    public IEnumerable<(string Path, WiiFstNode Node)> EnumerateFiles() => Enumerate(Root, string.Empty);

    public int GetSerializedSize()
    {
        Count(Root, out int entries, out int nameBytes);

        return Align4((entries + 1) * 12 + nameBytes);
    }

    public byte[] Serialize()
    {
        Count(Root, out int entries, out int nameBytes);

        int count = entries + 1;
        int tableSize = count * 12;
        byte[] fst = new byte[Align4(tableSize + nameBytes)];

        fst[0] = 1;

        BinaryPrimitives.WriteUInt32BigEndian(fst.AsSpan(8), (uint)count);

        int index = 1;
        int nameCursor = 0;

        Write(fst, tableSize, Root, 0, ref index, ref nameCursor);

        return fst;
    }

    private static void ParseChildren(byte[] fst, long tableEnd, ref int index, int end, WiiFstNode parent, int depth)
    {
        while (index < end)
        {
            int current = index;
            var entry = fst.AsSpan(current * 12, 12);
            bool isDirectory = entry[0] != 0;
            int nameOffset = (entry[1] << 16) | (entry[2] << 8) | entry[3];
            uint first = BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);
            uint second = BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
            long nameStart = tableEnd + nameOffset;

            if (nameStart >= fst.Length)
                throw new InvalidDataException("FST 이름 오프셋이 올바르지 않습니다.");

            int nameEnd = Array.IndexOf(fst, (byte)0, (int)nameStart);

            if (nameEnd < 0)
                nameEnd = fst.Length;

            var node = new WiiFstNode(fst.AsSpan((int)nameStart, nameEnd - (int)nameStart).ToArray(), isDirectory);

            parent.Children.Add(node);

            index++;

            if (isDirectory)
            {
                if (second <= current || second > end)
                    throw new InvalidDataException("FST 디렉터리 범위가 올바르지 않습니다.");

                if (depth >= MaximumDepth)
                    throw new InvalidDataException("FST 디렉터리 중첩이 너무 깊습니다.");

                ParseChildren(fst, tableEnd, ref index, (int)second, node, depth + 1);

                if (index != second)
                    throw new InvalidDataException("FST 디렉터리 구조가 올바르지 않습니다.");
            }
            else
            {
                node.Offset = (long)first << 2;
                node.Size = second;
            }
        }
    }

    private static IEnumerable<(string Path, WiiFstNode Node)> Enumerate(WiiFstNode directory, string prefix)
    {
        foreach (var child in directory.Children)
        {
            string path = prefix + "/" + child.Name;

            if (child.IsDirectory)
            {
                foreach (var item in Enumerate(child, path))
                    yield return item;
            }
            else
                yield return (path, child);
        }
    }

    private static void Count(WiiFstNode directory, out int entries, out int nameBytes)
    {
        entries = 0;
        nameBytes = 0;

        foreach (var child in directory.Children)
        {
            entries++;
            nameBytes += child.NameBytes.Length + 1;

            if (child.IsDirectory)
            {
                Count(child, out int childEntries, out int childNameBytes);

                entries += childEntries;
                nameBytes += childNameBytes;
            }
        }
    }

    private static void Write(byte[] fst, int tableSize, WiiFstNode directory, int directoryIndex, ref int index, ref int nameCursor)
    {
        foreach (var child in directory.Children)
        {
            int current = index++;
            int entryOffset = current * 12;

            if (nameCursor > MaximumNameOffset)
                throw new InvalidDataException("FST 이름 영역이 너무 큽니다.");

            fst[entryOffset] = child.IsDirectory ? (byte)1 : (byte)0;
            fst[entryOffset + 1] = (byte)(nameCursor >> 16);
            fst[entryOffset + 2] = (byte)(nameCursor >> 8);
            fst[entryOffset + 3] = (byte)nameCursor;

            child.NameBytes.CopyTo(fst, tableSize + nameCursor);

            nameCursor += child.NameBytes.Length + 1;

            if (child.IsDirectory)
            {
                Write(fst, tableSize, child, current, ref index, ref nameCursor);
                BinaryPrimitives.WriteUInt32BigEndian(fst.AsSpan(entryOffset + 4), (uint)directoryIndex);
                BinaryPrimitives.WriteUInt32BigEndian(fst.AsSpan(entryOffset + 8), (uint)index);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(fst.AsSpan(entryOffset + 4), (uint)(child.Offset >> 2));
                BinaryPrimitives.WriteUInt32BigEndian(fst.AsSpan(entryOffset + 8), (uint)child.Size);
            }
        }
    }

    private static int Align4(int value) => (value + 3) & ~3;
}