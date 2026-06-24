using PBP.Core.Constants;
using PBP.Core.Models;

namespace PBP.Core.Services;

public class SFOBuilder
{
    private readonly List<SfoEntry> _entries = [];

    public SFOBuilder() { }

    public SFOBuilder(IEnumerable<SfoEntry> entries) => _entries.AddRange(entries);

    public void AddEntry(string key, object value) => _entries.Add(new SfoEntry { Key = key, Value = value });

    public SfoFile Build()
    {
        var sfo = new SfoFile
        {
            Magic = 0x46535000,
            Version = 0x00000101,
            Entries = []
        };

        var validEntries = _entries
            .Where(entry => entry.Value != null && (entry.Value is string s && s.Trim().Length > 0) || (entry.Value is int))
            .ToList();

        var headerSize = 20;
        var indexTableSize = validEntries.Count * 16;
        var keyTableSize = validEntries.Sum(x => x.Key.Length + 1);

        if (keyTableSize % 4 != 0)
            sfo.Padding = (uint)(4 - keyTableSize % 4);

        sfo.KeyTableOffset = (uint)(headerSize + indexTableSize);
        sfo.DataTableOffset = sfo.KeyTableOffset + (uint)keyTableSize + sfo.Padding;

        ushort keyOffset = 0;
        uint dataOffset = 0;

        foreach (var entry in validEntries)
        {
            var entryLength = GetEntryLength(entry.Key, entry.Value!);
            var maxLength = GetMaxLength(entry.Key);

            if (entryLength > maxLength)
                throw new Exception($"Value for {entry.Key} exceeds maximum allowed length");

            sfo.Entries.Add(new SfoIndexEntry
            {
                KeyOffset = keyOffset,
                Format = GetEntryType(entry.Key),
                Length = entryLength,
                MaxLength = maxLength,
                DataOffset = dataOffset,
                Key = entry.Key,
                Value = entry.Value,
            });

            dataOffset += maxLength;
            keyOffset += (ushort)(entry.Key.Length + 1);
        }

        sfo.Size = sfo.DataTableOffset + dataOffset;

        return sfo;
    }

    private static uint GetMaxLength(string key) => key switch
    {
        SfoKeys.BOOTABLE => 4,
        SfoKeys.CATEGORY => 4,
        SfoKeys.DISC_ID => 16,
        SfoKeys.DISC_VERSION => 8,
        SfoKeys.LICENSE => 512,
        SfoKeys.PARENTAL_LEVEL => 4,
        SfoKeys.PSP_SYSTEM_VER => 8,
        SfoKeys.REGION => 4,
        SfoKeys.TITLE => 128,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static ushort GetEntryType(string key)
    {
        const ushort stringType = 0x0204;
        const ushort intType = 0x0404;

        return key switch
        {
            SfoKeys.BOOTABLE => intType,
            SfoKeys.CATEGORY => stringType,
            SfoKeys.DISC_ID => stringType,
            SfoKeys.DISC_VERSION => stringType,
            SfoKeys.LICENSE => stringType,
            SfoKeys.PARENTAL_LEVEL => intType,
            SfoKeys.PSP_SYSTEM_VER => stringType,
            SfoKeys.REGION => intType,
            SfoKeys.TITLE => stringType,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }

    private static ushort GetEntryLength(string key, object value)
    {
        ushort strlen = 0;

        if (value is string s)
            strlen = (ushort)(s.Length + 1);

        return key switch
        {
            SfoKeys.BOOTABLE => 4,
            SfoKeys.CATEGORY => strlen,
            SfoKeys.DISC_ID => strlen,
            SfoKeys.DISC_VERSION => strlen,
            SfoKeys.LICENSE => strlen,
            SfoKeys.PARENTAL_LEVEL => 4,
            SfoKeys.PSP_SYSTEM_VER => strlen,
            SfoKeys.REGION => 4,
            SfoKeys.TITLE => strlen,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }
}