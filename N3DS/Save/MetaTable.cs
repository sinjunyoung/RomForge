using N3DS.Save.Interfaces;
using N3DS.Save.Models;
using System.Buffers.Binary;

namespace N3DS.Save;

public class MetaTable
{
    private readonly IRandomAccessFile _hash;
    private readonly IRandomAccessFile _table;
    private readonly int _buckets;
    private readonly int _entryLen;
    private readonly int _eoInfo;
    private readonly int _eoCollision;
    private readonly int _keyLen;
    private readonly int _infoLen;
    private readonly Dictionary<uint, int> _refCount = [];
    private readonly Func<byte[], int, IParentedKey> _readKey;
    private readonly Func<byte[], int, IFileInfo> _readFileInfo;
    private readonly Func<byte[], int, IDirInfo> _readDirInfo;
    private readonly bool _isFileTable;

    public MetaTable(IRandomAccessFile hash, IRandomAccessFile table, int keyLen, int infoLen, Func<byte[], int, IParentedKey> readKey, Func<byte[], int, IFileInfo> readFileInfo)
    {
        if (hash.Length % 4 != 0)
            throw new InvalidOperationException("MetaTable: hash size not multiple of 4");

        _hash = hash;
        _table = table;
        _buckets = hash.Length / 4;
        _keyLen = keyLen;
        _infoLen = infoLen;
        _entryLen = keyLen + infoLen + 4;
        _eoInfo = keyLen;
        _eoCollision = keyLen + infoLen;
        _readKey = readKey;
        _readFileInfo = readFileInfo;
        _readDirInfo = null!;
        _isFileTable = true;
    }

    public MetaTable(IRandomAccessFile hash, IRandomAccessFile table, int keyLen, int infoLen, Func<byte[], int, IParentedKey> readKey, Func<byte[], int, IDirInfo> readDirInfo)
    {
        if (hash.Length % 4 != 0)
            throw new InvalidOperationException("MetaTable: hash size not multiple of 4");

        _hash = hash;
        _table = table;
        _buckets = hash.Length / 4;
        _keyLen = keyLen;
        _infoLen = infoLen;
        _entryLen = keyLen + infoLen + 4;
        _eoInfo = keyLen;
        _eoCollision = keyLen + infoLen;
        _readKey = readKey;
        _readFileInfo = null!;
        _readDirInfo = readDirInfo;
        _isFileTable = false;
    }

    public static void Format(IRandomAccessFile hash, IRandomAccessFile table, int keyLen, int infoLen, int entryCount)
    {
        var zeros = new byte[hash.Length];

        hash.Write(0, zeros, 0, zeros.Length);
        WriteU32(table, 0, 1);
        WriteU32(table, 4, (uint)entryCount);

        int padding = keyLen + infoLen - 8;

        if (padding > 0)
        {
            var padBuf = new byte[padding];

            table.Write(8, padBuf, 0, padding);
        }

        WriteU32(table, keyLen + infoLen, 0);
    }

    private uint HashKey(IParentedKey key)
    {
        var bytes = new byte[_keyLen];

        key.WriteTo(bytes, 0);

        uint h = 0x12345678;

        for (int i = 0; i < _keyLen; i += 4)
        {
            h = (h >> 1) | (h << 31);
            h ^= BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i));
        }

        return h % (uint)_buckets;
    }

    private static uint ReadU32(IRandomAccessFile f, int pos)
    {
        var buf = new byte[4];

        f.Read(pos, buf, 0, 4);

        return BinaryPrimitives.ReadUInt32LittleEndian(buf);
    }

    private static void WriteU32(IRandomAccessFile f, int pos, uint value)
    {
        var buf = new byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);

        f.Write(pos, buf, 0, 4);
    }

    private IParentedKey ReadKey(int entryOffset)
    {
        var buf = new byte[_keyLen];

        _table.Read(entryOffset, buf, 0, _keyLen);

        return _readKey(buf, 0);
    }

    private IFileInfo ReadFileInfo(int entryOffset)
    {
        var buf = new byte[_infoLen];

        _table.Read(entryOffset + _eoInfo, buf, 0, _infoLen);

        return _readFileInfo(buf, 0);
    }

    private IDirInfo ReadDirInfo(int entryOffset)
    {
        var buf = new byte[_infoLen];

        _table.Read(entryOffset + _eoInfo, buf, 0, _infoLen);

        return _readDirInfo(buf, 0);
    }

    private void WriteKey(int entryOffset, IParentedKey key)
    {
        var buf = new byte[_keyLen];

        key.WriteTo(buf, 0);
        _table.Write(entryOffset, buf, 0, _keyLen);
    }

    private void WriteFileInfo(int entryOffset, IFileInfo info)
    {
        var buf = new byte[_infoLen];

        info.WriteTo(buf, 0);
        _table.Write(entryOffset + _eoInfo, buf, 0, _infoLen);
    }

    private void WriteDirInfo(int entryOffset, IDirInfo info)
    {
        var buf = new byte[_infoLen];

        info.WriteTo(buf, 0);
        _table.Write(entryOffset + _eoInfo, buf, 0, _infoLen);
    }

    public (object info, uint index) Get(IParentedKey key)
    {
        uint h = HashKey(key);
        uint index = ReadU32(_hash, (int)(h * 4));

        while (index != 0)
        {
            int entryOffset = (int)index * _entryLen;
            var otherKey = ReadKey(entryOffset);

            if (key.Equals(otherKey))
            {
                object info = _isFileTable ? (object)ReadFileInfo(entryOffset) : ReadDirInfo(entryOffset);

                return (info, index);
            }

            index = ReadU32(_table, entryOffset + _eoCollision);
        }

        throw new FileNotFoundException("MetaTable::get: not found");
    }

    public (object info, IParentedKey key) GetAt(uint index)
    {
        int entryOffset = (int)index * _entryLen;
        object info = _isFileTable ? (object)ReadFileInfo(entryOffset) : ReadDirInfo(entryOffset);
        var key = ReadKey(entryOffset);

        return (info, key);
    }

    public void SetFileInfo(uint index, IFileInfo info)
    {
        int entryOffset = (int)index * _entryLen;

        WriteFileInfo(entryOffset, info);
    }

    public void SetDirInfo(uint index, IDirInfo info)
    {
        int entryOffset = (int)index * _entryLen;

        WriteDirInfo(entryOffset, info);
    }

    public void Remove(uint index)
    {
        int entryOffset = (int)index * _entryLen;
        var key = ReadKey(entryOffset);
        uint collision = ReadU32(_table, entryOffset + _eoCollision);
        uint h = HashKey(key);
        bool prevIsHash = true;
        int prevOffset = (int)(h * 4);

        while (true)
        {
            uint other = prevIsHash ? ReadU32(_hash, prevOffset) : ReadU32(_table, prevOffset);

            if (other == index)
            {
                if (prevIsHash)
                    WriteU32(_hash, prevOffset, collision);
                else
                    WriteU32(_table, prevOffset, collision);
                break;
            }

            prevIsHash = false;
            prevOffset = (int)other * _entryLen + _eoCollision;
        }

        var dummy = new byte[_entryLen];

        _table.Read(0, dummy, 0, _entryLen);
        _table.Write(entryOffset, dummy, 0, _entryLen);
        WriteU32(_table, _eoCollision, index);
    }

    public uint Add(IParentedKey key, object info)
    {
        try
        {
            Get(key);
            throw new InvalidOperationException("MetaTable::add: already exists");
        }
        catch (FileNotFoundException) { }

        uint index = ReadU32(_table, _eoCollision);
        int entryOffset;

        if (index == 0)
        {
            uint entryCount = ReadU32(_table, 0);
            uint maxEntryCount = ReadU32(_table, 4);

            if (entryCount == maxEntryCount)
                throw new InvalidOperationException("MetaTable::add: no space");

            WriteU32(_table, 0, entryCount + 1);

            index = entryCount;
            entryOffset = (int)index * _entryLen;
        }
        else
        {
            entryOffset = (int)index * _entryLen;

            uint nextDummy = ReadU32(_table, entryOffset + _eoCollision);

            WriteU32(_table, _eoCollision, nextDummy);
        }

        uint h = HashKey(key);
        uint oldHead = ReadU32(_hash, (int)(h * 4));

        WriteU32(_hash, (int)(h * 4), index);
        WriteKey(entryOffset, key);

        if (info is IFileInfo fi)
            WriteFileInfo(entryOffset, fi);
        else if (info is IDirInfo di)
            WriteDirInfo(entryOffset, di);

        WriteU32(_table, entryOffset + _eoCollision, oldHead);

        return index;
    }

    public MetaTableStat Stat()
    {
        uint entryCount = ReadU32(_table, 0);
        uint maxEntryCount = ReadU32(_table, 4);
        uint index = ReadU32(_table, _eoCollision);
        uint dummyCount = 0;

        while (index != 0)
        {
            dummyCount++;

            int entryOffset = (int)index * _entryLen;

            index = ReadU32(_table, entryOffset + _eoCollision);
        }
        return new MetaTableStat
        {
            Total = (int)maxEntryCount - 1,
            Free = (int)(maxEntryCount - entryCount + dummyCount),
        };
    }

    public uint AcquireTicket(uint index)
    {
        _refCount.TryGetValue(index, out int prev);
        _refCount[index] = prev + 1;

        return index;
    }

    public void ReleaseTicket(uint index)
    {
        if (_refCount.TryGetValue(index, out int prev))
        {
            if (prev == 1)
                _refCount.Remove(index);
            else
                _refCount[index] = prev - 1;
        }
    }

    public void CheckExclusive(uint index)
    {
        if (_refCount.TryGetValue(index, out int count) && count != 1)
            throw new InvalidOperationException("MetaTable: busy (not exclusive)");
    }
}