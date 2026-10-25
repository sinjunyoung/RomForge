using N3DS.Core.Models;

namespace N3DS.Core.FileSystem;

public class CiFinishManager
{
    private const string Magic = "CIFINISH";
    private const int CurrentVersion = 3;
    private const int EntrySize = 0x20;

    private readonly string _path;
    private readonly Dictionary<ulong, CiFinishEntry> _entries;

    public CiFinishManager(string sdRoot)
    {
        _path = Path.Combine(sdRoot, "cifinish.bin");
        _entries = Load();
    }

    public IReadOnlyDictionary<ulong, CiFinishEntry> Entries => _entries;

    private Dictionary<ulong, CiFinishEntry> Load()
    {
        if (!File.Exists(_path))
            return [];

        using var f = File.OpenRead(_path);

        if (f.Length < 16)
            return [];

        using var br = new BinaryReader(f);
        string magic = new(br.ReadChars(8));

        if (magic != Magic)
            throw new InvalidDataException("cifinish.bin 매직 불일치");

        uint version = br.ReadUInt32();
        uint count = br.ReadUInt32();

        if (version != CurrentVersion)
            throw new InvalidDataException($"cifinish.bin 버전 불일치: {version}");

        var result = new Dictionary<ulong, CiFinishEntry>();
        ReadOnlySpan<byte> titleMagic = "TITLE\0"u8;

        for (int i = 0; i < count; i++)
        {
            byte[] raw = br.ReadBytes(EntrySize);

            if (raw.Length != EntrySize)
                break;

            if (!raw.AsSpan(0, 6).SequenceEqual(titleMagic))
                continue;

            bool hasSeed = raw[0x06] != 0;
            ulong titleId = BitConverter.ToUInt64(raw, 0x08);
            byte[]? seed = hasSeed ? raw[0x10..0x20] : null;

            result[titleId] = new CiFinishEntry { TitleId = titleId, Seed = seed };
        }

        return result;
    }

    public void AddOrUpdate(ulong titleId, byte[]? seed = null)
    {
        _entries[titleId] = new CiFinishEntry { TitleId = titleId, Seed = seed };

        Save();
    }

    public void Remove(ulong titleId)
    {
        if (_entries.Remove(titleId))
            Save();
    }

    public bool Contains(ulong titleId) => _entries.ContainsKey(titleId);

    private void Save()
    {
        using var f = File.Create(_path);
        using var bw = new BinaryWriter(f);
        var entries = _entries.Values.OrderBy(e => e.TitleId).ToList();

        bw.Write(Magic.ToCharArray());
        bw.Write((uint)CurrentVersion);
        bw.Write((uint)entries.Count);

        foreach (var entry in entries)
        {
            byte[] raw = new byte[EntrySize];

            System.Text.Encoding.ASCII.GetBytes("TITLE\0").CopyTo(raw, 0);

            raw[0x06] = entry.Seed is not null ? (byte)1 : (byte)0;
            raw[0x07] = 0;

            BitConverter.GetBytes(entry.TitleId).CopyTo(raw, 0x08);
            entry.Seed?.CopyTo(raw, 0x10);
            bw.Write(raw);
        }
    }
}