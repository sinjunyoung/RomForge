using System.Text;

namespace Patch.Core.Services.PC98;

public sealed class Fat16DirEntry
{
    public required string Name { get; init; }
    public required byte Attr { get; init; }
    public ushort Cluster { get; set; }
    public uint Size { get; set; }
    public required int EntryOffset { get; init; }

    public bool IsDirectory => (Attr & 0x10) != 0;
}

public sealed class Fat16Image
{
    private readonly byte[] _raw;
    private int _bps;
    private int _spc;
    private int _reserved;
    private int _nfats;
    private int _rootEnt;
    private int _spf;
    private int _fatOff;
    private int _rootOff;
    private int _rootDirBytes;
    private int _dataOff;
    private int _clSize;

    public Fat16Image(string path)
    {
        _raw = File.ReadAllBytes(path);
        LocateVolume();
    }

    private void LocateVolume()
    {
        int n = _raw.Length;

        for (int off = 0; off <= n - 512; off += 512)
        {
            byte b0 = _raw[off];
            byte b510 = _raw[off + 510];
            byte b511 = _raw[off + 511];

            if (b510 != 0x55 || b511 != 0xAA || (b0 != 0xEB && b0 != 0xE9))
                continue;

            int bps = BitConverter.ToUInt16(_raw, off + 0x0B);

            if (bps is not (256 or 512 or 1024 or 2048))
                continue;

            string fatName = Encoding.ASCII.GetString(_raw, off + 0x36, 8);

            if (!fatName.Contains("FAT"))
                continue;

            ParseBpb(off);
            return;
        }

        throw new InvalidDataException("FAT16 볼륨을 찾을 수 없습니다.");
    }

    private void ParseBpb(int bootOff)
    {
        _bps = BitConverter.ToUInt16(_raw, bootOff + 0x0B);
        _spc = _raw[bootOff + 0x0D];
        _reserved = BitConverter.ToUInt16(_raw, bootOff + 0x0E);
        _nfats = _raw[bootOff + 0x10];
        _rootEnt = BitConverter.ToUInt16(_raw, bootOff + 0x11);
        _spf = BitConverter.ToUInt16(_raw, bootOff + 0x16);
        _rootDirBytes = _rootEnt * 32;
        _clSize = _spc * _bps;

        int partOff = CalibratePartOff(bootOff);

        _fatOff = partOff + _reserved * _bps;
        _rootOff = _fatOff + _nfats * _spf * _bps;
        _dataOff = _rootOff + _rootDirBytes;
    }

    private int CalibratePartOff(int bootOff)
    {
        int[] deltas = [0, -512, 512, -1024, 1024, -256, 256];

        foreach (int delta in deltas)
        {
            int cand = bootOff + delta;

            if (cand < 0)
                continue;

            int fatOff = cand + _reserved * _bps;
            int rootOff = fatOff + _nfats * _spf * _bps;

            if (rootOff + 32 > _raw.Length)
                continue;

            byte first = _raw[rootOff];

            if (first is 0x00 or 0xE5)
                continue;

            bool nameOk = true;

            for (int i = 0; i < 8; i++)
            {
                byte b = _raw[rootOff + i];

                if (b != 0x20 && (b < 32 || b >= 127))
                {
                    nameOk = false;
                    break;
                }
            }

            byte attr = _raw[rootOff + 11];
            bool attrOk = attr is 0x00 or 0x01 or 0x02 or 0x04 or 0x08 or 0x10 or 0x20 or 0x27;

            if (nameOk && attrOk)
                return cand;
        }

        return bootOff;
    }

    private ushort ReadFat(int cluster) => BitConverter.ToUInt16(_raw, _fatOff + cluster * 2);

    private void WriteFat(int cluster, ushort value) => BitConverter.GetBytes(value).CopyTo(_raw, _fatOff + cluster * 2);

    private int ClusterOffset(int cluster) => _dataOff + (cluster - 2) * _clSize;

    private List<int> Chain(int start)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        int c = start;

        while (c is < 0xFFF8 and not 0 && seen.Add(c))
        {
            result.Add(c);
            c = ReadFat(c);
        }

        return result;
    }

    public byte[] ReadFile(Fat16DirEntry entry)
    {
        var chain = Chain(entry.Cluster);
        var buf = new byte[chain.Count * _clSize];

        for (int i = 0; i < chain.Count; i++)
            Buffer.BlockCopy(_raw, ClusterOffset(chain[i]), buf, i * _clSize, _clSize);

        return buf[..(int)entry.Size];
    }

    private Fat16DirEntry ParseEntry(int off)
    {
        string name = Encoding.ASCII.GetString(_raw, off, 8).Trim();
        string ext = Encoding.ASCII.GetString(_raw, off + 8, 3).Trim();
        byte attr = _raw[off + 11];
        ushort cluster = BitConverter.ToUInt16(_raw, off + 26);
        uint size = BitConverter.ToUInt32(_raw, off + 28);
        string fileName = ext.Length > 0 ? $"{name}.{ext}" : name;

        return new Fat16DirEntry { Name = fileName, Attr = attr, Cluster = cluster, Size = size, EntryOffset = off };
    }

    private List<Fat16DirEntry> ListRoot()
    {
        var entries = new List<Fat16DirEntry>();

        for (int i = 0; i < _rootDirBytes; i += 32)
        {
            int off = _rootOff + i;
            byte first = _raw[off];

            if (first == 0x00)
                break;

            if (first == 0xE5 || _raw[off + 11] == 0x0F)
                continue;

            entries.Add(ParseEntry(off));
        }

        return entries;
    }

    private List<Fat16DirEntry> ListSubdir(int startCluster)
    {
        var entries = new List<Fat16DirEntry>();
        bool stop = false;

        foreach (int c in Chain(startCluster))
        {
            int baseOff = ClusterOffset(c);

            for (int i = 0; i < _clSize; i += 32)
            {
                int off = baseOff + i;
                byte first = _raw[off];

                if (first == 0x00)
                {
                    stop = true;
                    break;
                }

                if (first == 0xE5 || _raw[off + 11] == 0x0F)
                    continue;

                entries.Add(ParseEntry(off));
            }

            if (stop)
                break;
        }

        return entries;
    }

    public Dictionary<string, Fat16DirEntry> Walk()
    {
        var result = new Dictionary<string, Fat16DirEntry>(StringComparer.OrdinalIgnoreCase);

        void Recurse(List<Fat16DirEntry> entries, string prefix)
        {
            foreach (var e in entries)
            {
                if (e.Name is "." or "..")
                    continue;

                string path = prefix + e.Name;

                if (e.IsDirectory)
                {
                    result[path + "/"] = e;
                    Recurse(ListSubdir(e.Cluster), path + "/");
                }
                else
                    result[path] = e;
            }
        }

        Recurse(ListRoot(), string.Empty);

        return result;
    }

    public void ReplaceFile(Fat16DirEntry entry, byte[] newData)
    {
        var oldChain = Chain(entry.Cluster);
        int need = Math.Max(1, (newData.Length + _clSize - 1) / _clSize);
        List<int> chain;

        if (need <= oldChain.Count)
        {
            chain = [.. oldChain.Take(need)];

            foreach (int c in oldChain.Skip(need))
                WriteFat(c, 0x0000);
        }
        else
        {
            var extra = FreeClusters(need - oldChain.Count, [.. oldChain]);
            chain = [.. oldChain, .. extra];
        }

        for (int i = 0; i < chain.Count; i++)
        {
            ushort next = (ushort)(i + 1 < chain.Count ? chain[i + 1] : 0xFFFF);
            WriteFat(chain[i], next);
        }

        var padded = new byte[chain.Count * _clSize];
        Buffer.BlockCopy(newData, 0, padded, 0, newData.Length);

        for (int i = 0; i < chain.Count; i++)
            Buffer.BlockCopy(padded, i * _clSize, _raw, ClusterOffset(chain[i]), _clSize);

        BitConverter.GetBytes((uint)newData.Length).CopyTo(_raw, entry.EntryOffset + 28);
        entry.Cluster = (ushort)chain[0];
        entry.Size = (uint)newData.Length;
    }

    private List<int> FreeClusters(int count, HashSet<int> avoid)
    {
        var found = new List<int>();
        int totalClusters = (_raw.Length - _dataOff) / _clSize;

        for (int c = 2; c < totalClusters + 2 && found.Count < count; c++)
        {
            if (!avoid.Contains(c) && ReadFat(c) == 0x0000)
                found.Add(c);
        }

        if (found.Count < count)
            throw new InvalidOperationException("빈 클러스터가 부족합니다.");

        return found;
    }

    public void Save(string path) => File.WriteAllBytes(path, _raw);
}