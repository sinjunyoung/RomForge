using N3DS.Save.Enums;
using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class Db : IFileSystem
{
    private readonly DbInner _center;

    public Db(IRandomAccessFile file, DbType dbType, byte[] key)
    {
        uint signerId = dbType switch
        {
            DbType.Ticket => 0,
            DbType.SdTitle or DbType.NandTitle => 2,
            DbType.SdImport or DbType.NandImport => 3,
            DbType.TmpTitle => 4,
            DbType.TmpImport => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null)
        };

        var signer = ((ISigner)new DbSigner(signerId), key);
        var diff = new Diff(file, signer);
        int preLen = dbType == DbType.Ticket ? 0x10 : 0x80;
        var magic = new byte[dbType == DbType.Ticket ? 4 : 8];

        diff.Partition.Read(0, magic, 0, magic.Length);

        if (dbType == DbType.Ticket)
        {
            if (!magic.AsSpan().SequenceEqual("TICK"u8))
                throw new InvalidDataException("Db: unexpected TICK magic");
        }
        else
        {
            ReadOnlySpan<byte> expected = dbType switch
            {
                DbType.NandTitle => "NANDTDB\0"u8,
                DbType.NandImport => "NANDIDB\0"u8,
                DbType.TmpTitle => "TEMPIDB\0"u8,
                DbType.TmpImport => "TEMPIDB\0"u8,
                DbType.SdTitle => "TEMPTDB\0"u8,
                DbType.SdImport => "TEMPTDB\0"u8,
                _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null)
            };

            if (!magic.AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("Db: unexpected database magic");
        }

        var withoutPre = new SubFile(diff.Partition, preLen, diff.Partition.Length - preLen);
        var header = DbHeader.Read(withoutPre, 0);

        if (!header.Magic.AsSpan().SequenceEqual("BDRI"u8) || header.Version != 0x30000)
            throw new InvalidDataException("Db: unexpected BDRI magic/version");


        var fsInfo = FsInfo.Read(withoutPre, (int)header.FsInfoOffset);

        if (fsInfo.DataBlockCount != fsInfo.FatSize)
            throw new InvalidDataException("Db: data_block_count != fat_size");

        var dirHash = new SubFile(withoutPre, (int)fsInfo.DirHashOffset, (int)fsInfo.DirBuckets * 4);
        var fileHash = new SubFile(withoutPre, (int)fsInfo.FileHashOffset, (int)fsInfo.FileBuckets * 4);
        var fatTable = new SubFile(withoutPre, (int)fsInfo.FatOffset, ((int)fsInfo.FatSize + 1) * 8);
        int dataOffset = (int)fsInfo.DataOffset;
        int dataLen = (int)(fsInfo.DataBlockCount * fsInfo.BlockLen);
        int dataEnd = dataLen + dataOffset;
        int dataDelta = withoutPre.Length < dataEnd ? dataEnd - withoutPre.Length : 0;
        IRandomAccessFile data = new FakeSizeFile(new SubFile(withoutPre, dataOffset, dataLen - dataDelta), dataLen);
        var fat = new Fat(fatTable, data, (int)fsInfo.BlockLen);
        var dirTableFile = fat.Open((int)fsInfo.DirTable.BlockIndex);
        var fileTableFile = fat.Open((int)fsInfo.FileTable.BlockIndex);
        var dirMetaTable = new MetaTable(dirHash, dirTableFile, DbDirKey.KeySize, DbDirInfo.InfoSize, (buf, off) => { var k = new DbDirKey(); k.ReadFrom(buf, off); return k; }, (buf, off) => { var i = new DbDirInfo(); i.ReadFrom(buf, off); return i; });
        var fileMetaTable = new MetaTable(fileHash, fileTableFile, DbFileKey.KeySize, DbFileInfo.InfoSize, (buf, off) => { var k = new DbFileKey(); k.ReadFrom(buf, off); return k; }, (buf, off) => { var i = new DbFileInfo(); i.ReadFrom(buf, off); return i; });
        var fs = new FsMeta(dirMetaTable, fileMetaTable);

        _center = new DbInner(diff, fat, fs, (int)fsInfo.BlockLen, (int)fsInfo.DataBlockCount);
    }

    public IFileSystemDir OpenRoot() => OpenDir(1);

    public IFileSystemFile OpenFile(uint ino)
    {
        var meta = new FileMeta(_center.Fs, ino);

        return DbFile.FromMeta(_center, meta);
    }

    public IFileSystemDir OpenDir(uint ino)
    {
        var meta = new DirMeta(_center.Fs, ino);

        return new DbDir(_center, meta);
    }

    public void Commit() => _center.Diff.Commit();

    public FsStat Stat()
    {
        var metaStat = _center.Fs.Stat();

        return new FsStat
        {
            BlockLen = _center.BlockLen,
            TotalBlocks = _center.BlockCount,
            FreeBlocks = _center.Fat.FreeBlocks,
            TotalFiles = metaStat.Files.Total,
            FreeFiles = metaStat.Files.Free,
            TotalDirs = metaStat.Dirs.Total,
            FreeDirs = metaStat.Dirs.Free,
        };
    }
}