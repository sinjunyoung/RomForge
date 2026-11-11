using System.Buffers.Binary;
using Common.Localization;
using System.Security.Cryptography;

namespace N3DS.Core.Crypto;

public class KeyStore
{
    private readonly KeySlot[] _slots = new KeySlot[0x40];
    private readonly byte[]?[] _commonKeys = new byte[]?[6];
    private byte[]? _sdKeyY;
    private readonly Dictionary<ulong, byte[]> _seeds = [];

    public KeyStore()
    {
        for (int i = 0; i < _slots.Length; i++)
            _slots[i] = new KeySlot();
        
        TryLoadAesKeys(KeyStorePaths.AesKeys);
        TryLoadBoot9(KeyStorePaths.Boot9);
        TryLoadSeedDb(KeyStorePaths.SeedDb);
    }

    private void TryLoadAesKeys(string path)
    {
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                continue;

            var parts = trimmed.Split('=', 2);
            var key = parts[0].Trim().ToLowerInvariant();
            var value = parts[1].Trim();

            if (key.StartsWith("slot0x") && int.TryParse(key[6..8], System.Globalization.NumberStyles.HexNumber, null, out int slot))
            {
                if (key.EndsWith("keyx")) _slots[slot].SetKeyX(Convert.FromHexString(value));
                else if (key.EndsWith("keyy")) _slots[slot].SetKeyY(Convert.FromHexString(value));
                else if (key.EndsWith("keyn")) _slots[slot].SetNormalKey(Convert.FromHexString(value));
            }
            else if (key.StartsWith("commonkey") && int.TryParse(key["commonkey".Length..], out int cidx) && (uint)cidx < 6)
                _commonKeys[cidx] = Convert.FromHexString(value);
            else if (key.StartsWith("common") && int.TryParse(key["common".Length..], out int cidx2) && (uint)cidx2 < 6)
                _commonKeys[cidx2] = Convert.FromHexString(value);
        }
    }

    private void TryLoadBoot9(string path)
    {
        if (!File.Exists(path))
            return;

        byte[] boot9 = File.ReadAllBytes(path);
        int keyBlobOffset = boot9.Length == 0x10000 ? 0xD860 : 0x5860;
        byte[] keyX = boot9[(keyBlobOffset + 0x180)..(keyBlobOffset + 0x190)];

        _slots[0x30].SetKeyX(keyX);
    }

    private void TryLoadSeedDb(string path)
    {
        if (!File.Exists(path))
            return;

        byte[] data = File.ReadAllBytes(path);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
        int expectedLength = 16 + (int)count * 32;

        if (data.Length >= expectedLength)
        {
            for (int i = 0; i < count; i++)
            {
                int offset = 16 + i * 32;
                ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
                byte[] seed = data.AsSpan(offset + 8, 16).ToArray();

                _seeds[titleId] = seed;
            }
        }
        else
        {
            int pairCount = data.Length / 32;

            for (int i = 0; i < pairCount; i++)
            {
                int offset = i * 32;
                ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
                byte[] seed = data.AsSpan(offset + 8, 16).ToArray();

                _seeds[titleId] = seed;
            }
        }
    }

    public void LoadMovable(string path)
    {
        byte[] movable = File.ReadAllBytes(path);

        if (movable.Length < 0x120)
            throw new InvalidDataException(Loc.S.ErrMovableTooShort);

        if (!movable.AsSpan()[0..4].SequenceEqual("SEED"u8.ToArray()))
            throw new InvalidDataException(Loc.S.ErrMovableMagicMismatch);

        _sdKeyY = movable[0x110..0x120];
        _slots[0x34].SetKeyY(_sdKeyY);
        _slots[0x30].SetKeyY(_sdKeyY);
    }

    public string GetId0Hex()
    {
        byte[] hash = SHA256.HashData(_sdKeyY!);
        uint w0 = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(0x00));
        uint w1 = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(0x04));
        uint w2 = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(0x08));
        uint w3 = BinaryPrimitives.ReadUInt32LittleEndian(hash.AsSpan(0x0C));

        return $"{w0:x8}{w1:x8}{w2:x8}{w3:x8}";
    }

    public byte[]? GetSeed(ulong titleId)
    {
        if (!_seeds.TryGetValue(titleId, out var seed))
            return null;

        return seed;
    }

    public void SetKeyY(int slotId, byte[] keyY) => _slots[slotId].SetKeyY(keyY);

    public byte[] GetNormalKey(int slotId) => _slots[slotId].NormalKey ?? throw new InvalidOperationException(Loc.S.ErrSlotNormalKeyMissing(slotId.ToString("X2")));

    public byte[] DeriveNormalKey(int slotId, byte[] keyY)
    {
        byte[] keyX = _slots[slotId].KeyX ?? throw new InvalidOperationException(Loc.S.ErrSlotKeyXMissing(slotId.ToString("X2")));

        return KeySlot.ComputeNormalKey(keyX, keyY);
    }

    public byte[] GetSdKey() => GetNormalKey(0x34);

    public byte[] GetCommonKey(int index)
    {
        if ((uint)index >= _commonKeys.Length || _commonKeys[index] is null)
            throw new InvalidOperationException(Loc.S.ErrCommonKeyMissing(index));

        return _commonKeys[index]!;
    }

    public bool IsMovableLoaded => _sdKeyY is not null;
}