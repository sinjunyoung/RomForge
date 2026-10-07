using Patch.Core.Formats.DCP.Models;
using System.Text;


namespace Patch.Core.Formats.DCP.Services;

public class Iso9660Builder(byte[] originalPvd, Iso9660Entry root)
{
    private const int SectorSize = 2048;

    public byte[] OriginalPvd { get; } = originalPvd;
    public Iso9660Entry Root { get; } = root;

    private readonly Dictionary<Iso9660Entry, byte[]> _fileData = [];
    private uint _nextLba;

    public uint PathTableSize { get; private set; }
    public uint LPathTableLba { get; private set; }
    public uint MPathTableLba { get; private set; }
    private byte[] _lPathTableSectors = [];
    private byte[] _mPathTableSectors = [];

    public void SetFileData(Iso9660Entry entry, byte[] data) => _fileData[entry] = data;

    private static int SectorsFor(long size) => (int)Math.Ceiling(size / (double)SectorSize);

    public (uint RootLba, uint RootSize, uint TotalSectors) Relayout(uint startLba)
    {
        _nextLba = startLba;

        Root.ParentLba = 0;

        var pathOrder = BuildPathTableOrder(Root);
        var (lRawPlaceholder, _) = BuildPathTables(pathOrder, placeholderExtents: true);

        PathTableSize = (uint)lRawPlaceholder.Length;
        int pathTableSectors = SectorsFor(PathTableSize);

        LPathTableLba = _nextLba;
        _nextLba += (uint)pathTableSectors;
        MPathTableLba = _nextLba;
        _nextLba += (uint)pathTableSectors;

        AssignDirectorySectors(Root);
        LayoutContent(Root, isRoot: true);

        var (lFinal, mFinal) = BuildPathTables(pathOrder, placeholderExtents: false);

        _lPathTableSectors = PadToSector(lFinal);
        _mPathTableSectors = PadToSector(mFinal);

        return (Root.LayoutLba, Root.LayoutSize, _nextLba - startLba);
    }

    public IEnumerable<(uint Lba, byte[] Data)> GetPathTableSectors()
    {
        yield return (LPathTableLba, _lPathTableSectors);
        yield return (MPathTableLba, _mPathTableSectors);
    }

    private record PathTableOrderEntry(Iso9660Entry Dir, int Number, int ParentNumber);

    private static List<PathTableOrderEntry> BuildPathTableOrder(Iso9660Entry root)
    {
        var result = new List<PathTableOrderEntry>();
        var numberOf = new Dictionary<Iso9660Entry, int> { [root] = 1 };

        result.Add(new PathTableOrderEntry(root, 1, 1));

        var currentLevel = new List<Iso9660Entry> { root };
        int nextNumber = 2;

        while (currentLevel.Count > 0)
        {
            var nextLevel = new List<Iso9660Entry>();

            foreach (var dir in currentLevel)
            {
                var children = dir.Children.Where(c => c.IsDirectory).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();

                foreach (var child in children)
                {
                    int childNumber = nextNumber++;

                    numberOf[child] = childNumber;
                    result.Add(new PathTableOrderEntry(child, childNumber, numberOf[dir]));
                    nextLevel.Add(child);
                }
            }

            currentLevel = nextLevel;
        }

        return result;
    }

    private static (byte[] LTable, byte[] MTable) BuildPathTables(List<PathTableOrderEntry> order, bool placeholderExtents)
    {
        using var lMs = new MemoryStream();
        using var mMs = new MemoryStream();

        foreach (var entry in order)
        {
            bool isRoot = entry.Number == 1;
            byte[] nameBytes = isRoot ? [0x00] : Encoding.ASCII.GetBytes(entry.Dir.Name);
            byte lenDi = (byte)nameBytes.Length;
            uint extentLba = placeholderExtents ? 0u : entry.Dir.LayoutLba;
            ushort parentNumber = (ushort)entry.ParentNumber;

            WritePathTableRecord(lMs, lenDi, extentLba, parentNumber, nameBytes, bigEndian: false);
            WritePathTableRecord(mMs, lenDi, extentLba, parentNumber, nameBytes, bigEndian: true);
        }

        return (lMs.ToArray(), mMs.ToArray());
    }

    private static void WritePathTableRecord(MemoryStream ms, byte lenDi, uint extentLba, ushort parentNumber, byte[] nameBytes, bool bigEndian)
    {
        ms.WriteByte(lenDi);
        ms.WriteByte(0);

        var lbaBytes = BitConverter.GetBytes(extentLba);

        if (bigEndian)
            Array.Reverse(lbaBytes);

        ms.Write(lbaBytes, 0, 4);

        var parentBytes = BitConverter.GetBytes(parentNumber);

        if (bigEndian)
            Array.Reverse(parentBytes);

        ms.Write(parentBytes, 0, 2);
        ms.Write(nameBytes, 0, nameBytes.Length);

        if (nameBytes.Length % 2 != 0)
            ms.WriteByte(0);
    }

    private static byte[] PadToSector(byte[] data)
    {
        int sectors = (int)Math.Ceiling(data.Length / (double)SectorSize);

        if (data.Length == sectors * SectorSize)
            return data;

        var padded = new byte[sectors * SectorSize];

        Buffer.BlockCopy(data, 0, padded, 0, data.Length);

        return padded;
    }

    private static void AssignDirectorySectors(Iso9660Entry dir)
    {
        dir.LayoutSize = (uint)PackedDirectorySize(dir);

        foreach (var child in dir.Children.Where(c => c.IsDirectory))
            AssignDirectorySectors(child);
    }

    private static int PackedDirectorySize(Iso9660Entry dir)
    {
        int offsetInSector = 0;
        int totalSectors = 1;

        void Advance(int len)
        {
            if (offsetInSector + len > SectorSize)
            {
                totalSectors++;
                offsetInSector = 0;
            }
            offsetInSector += len;
        }

        Advance(DirRecordSize(".", isSelf: true, isDir: true));
        Advance(DirRecordSize("..", isSelf: true, isDir: true));

        foreach (var child in dir.Children)
            Advance(DirRecordSize(child.Name, isSelf: false, isDir: child.IsDirectory));

        return totalSectors * SectorSize;
    }

    private void LayoutContent(Iso9660Entry dir, bool isRoot = false)
    {
        dir.LayoutLba = _nextLba;
        _nextLba += (uint)SectorsFor(dir.LayoutSize);

        if (isRoot)
        {
            dir.ParentLba = dir.LayoutLba;
            dir.ParentSize = dir.LayoutSize;
        }

        foreach (var child in dir.Children.Where(c => !c.IsDirectory))
        {
            var data = _fileData.TryGetValue(child, out var d) ? d : throw new InvalidOperationException($"파일 데이터 미등록: {child.FullPath}");
            child.LayoutLba = _nextLba;
            child.LayoutSize = (uint)data.Length;
            _nextLba += (uint)SectorsFor(data.Length);
        }

        foreach (var child in dir.Children.Where(c => c.IsDirectory))
        {
            child.ParentLba = dir.LayoutLba;
            child.ParentSize = dir.LayoutSize;
            LayoutContent(child);
        }
    }

    private static int DirRecordSize(string name, bool isSelf, bool isDir)
    {
        int nameLen = isSelf ? 1 : Encoding.ASCII.GetByteCount(name + (isDir ? string.Empty : ";1"));
        int len = 33 + nameLen;

        if (len % 2 != 0)
            len++;

        return len;
    }

    public static byte[] BuildDirectoryRecordData(Iso9660Entry dir)
    {
        var buffer = new byte[dir.LayoutSize];
        int pos = 0;

        int WriteWithBoundaryCheck(string name, uint lba, uint size, bool isDir)
        {
            int len = DirRecordSize(name, name is "." or "..", isDir);
            int sectorOffset = pos % SectorSize;

            if (sectorOffset + len > SectorSize)
                pos += SectorSize - sectorOffset;

            return WriteDirRecord(buffer, pos, name, lba, size, isDir);
        }

        pos = WriteWithBoundaryCheck(".", dir.LayoutLba, dir.LayoutSize, true);
        pos = WriteWithBoundaryCheck("..", dir.ParentLba, dir.ParentSize, true);

        foreach (var child in dir.Children)
            pos = WriteWithBoundaryCheck(child.Name, child.LayoutLba, child.LayoutSize, child.IsDirectory);

        return buffer;
    }

    private static int WriteDirRecord(byte[] buffer, int pos, string name, uint lba, uint size, bool isDir)
    {
        var nameBytes = name is "." or ".." ? [(byte)(name == "." ? 0x00 : 0x01)] : Encoding.ASCII.GetBytes(name + (isDir ? string.Empty : ";1"));
        int recordLen = 33 + nameBytes.Length;

        if (recordLen % 2 != 0)
            recordLen++;

        buffer[pos + 0] = (byte)recordLen;
        buffer[pos + 1] = 0;

        WriteBothEndian32(buffer, pos + 2, lba);
        WriteBothEndian32(buffer, pos + 10, size);

        buffer[pos + 25] = (byte)(isDir ? 0x02 : 0x00);
        buffer[pos + 26] = 0;
        buffer[pos + 27] = 0;

        WriteBothEndian16(buffer, pos + 28, 1);

        buffer[pos + 32] = (byte)nameBytes.Length;

        Buffer.BlockCopy(nameBytes, 0, buffer, pos + 33, nameBytes.Length);

        return pos + recordLen;
    }

    private static void WriteBothEndian32(byte[] buffer, int offset, uint value)
    {
        var le = BitConverter.GetBytes(value);
        var be = (byte[])le.Clone();

        Array.Reverse(be);
        Buffer.BlockCopy(le, 0, buffer, offset, 4);
        Buffer.BlockCopy(be, 0, buffer, offset + 4, 4);
    }

    private static void WriteBothEndian16(byte[] buffer, int offset, ushort value)
    {
        var le = BitConverter.GetBytes(value);
        var be = (byte[])le.Clone();

        Array.Reverse(be);
        Buffer.BlockCopy(le, 0, buffer, offset, 2);
        Buffer.BlockCopy(be, 0, buffer, offset + 2, 2);
    }

    public byte[] BuildPvd(uint totalSectors)
    {
        var pvd = (byte[])OriginalPvd.Clone();

        WriteBothEndian32(pvd, 80, totalSectors);
        WriteBothEndian32(pvd, 156 + 2, Root.LayoutLba);
        WriteBothEndian32(pvd, 156 + 10, Root.LayoutSize);

        WriteBothEndian32(pvd, 132, PathTableSize);

        var lLba = BitConverter.GetBytes(LPathTableLba);
        Buffer.BlockCopy(lLba, 0, pvd, 140, 4);
        Array.Clear(pvd, 144, 4);

        var mLba = BitConverter.GetBytes(MPathTableLba);
        Array.Reverse(mLba);
        Buffer.BlockCopy(mLba, 0, pvd, 148, 4);
        Array.Clear(pvd, 152, 4);

        return pvd;
    }
}