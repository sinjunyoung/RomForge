using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class DbFile : IFileSystemFile
{
    private readonly DbInner _center;

    private readonly FileMeta _meta;

    private FatFile? _data;

    private int _len;

    public int Length => _len;

    private DbFile(DbInner center, FileMeta meta, FatFile? data, int len)
    {
        _center = center;
        _meta = meta;
        _data = data;
        _len = len;
    }

    public static DbFile FromMeta(DbInner center, FileMeta meta)
    {
        var info = (DbFileInfo)meta.GetInfo();
        int len = (int)info.Size;
        FatFile? data;

        if (info.Block == 0x80000000)
        {
            if (len != 0)
                throw new InvalidDataException("DbFile: non-empty file with invalid pointer");

            data = null;
        }
        else
        {
            var fatFile = center.Fat.Open((int)info.Block);

            if (len == 0 || len > fatFile.Length)
                throw new InvalidDataException("DbFile: size/pointer mismatch");

            data = fatFile;
        }

        return new DbFile(center, meta, data, len);
    }

    public uint GetParentIno() => _meta.GetParentIno();

    public uint GetIno() => _meta.Ino;

    public void Delete()
    {
        _data?.Delete();
        _meta.Delete();
    }

    public void Resize(int len)
    {
        if (len == _len) 
            return;

        _meta.CheckExclusive();

        var info = (DbFileInfo)_meta.GetInfo();

        if (_len == 0)
        {
            var (fatFile, block) = _center.Fat.Create(Misc.DivideUp(len, _center.BlockLen));

            _data = fatFile;
            info.Block = (uint)block;
        }
        else if (len == 0)
        {
            _data!.Delete();
            _data = null;
            info.Block = 0x80000000;
        }
        else
            _center.Fat.Resize(_data!, Misc.DivideUp(len, _center.BlockLen));

        info.Size = (ulong)len;
        _meta.SetInfo(info);
        _len = len;
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("DbFile::read out of bound");

        _data!.Read(pos, buf, offset, count);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("DbFile::write out of bound");

        _data!.Write(pos, buf, offset, count);
    }

    public void Commit() { }

    public void Rename(DbDir parent, ulong name)
    {
        try 
        { 
            parent.OpenSubFile(name); 
            throw new InvalidOperationException("DbFile::rename: already exists");
        }
        catch (FileNotFoundException) { }

        var newKey = DbFileKey.New(parent.Meta.Ino, name);

        _meta.Rename(parent.Meta, newKey);
    }
}