using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class DirMeta : IDisposable
{
    private uint _ino;
    private readonly FsMeta _fs;
    private bool _disposed;

    public uint Ino => _ino;

    public DirMeta(FsMeta fs, uint ino)
    {
        _fs = fs;
        _ino = ino;
        _fs.Dirs.AcquireTicket(ino);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fs.Dirs.ReleaseTicket(_ino);
            _disposed = true;
        }
    }

    public void CheckExclusive() => _fs.Dirs.CheckExclusive(_ino);

    public uint GetParentIno()
    {
        var (_, key) = _fs.Dirs.GetAt(_ino);

        return key.GetParent();
    }

    public IDirInfo GetInfo() => (IDirInfo)_fs.Dirs.GetAt(_ino).info;

    public DirMeta OpenSubDir(IParentedKey name)
    {
        var (_, pos) = _fs.Dirs.Get(name);

        return new DirMeta(_fs, pos);
    }

    public FileMeta OpenSubFile(IParentedKey name)
    {
        var (_, pos) = _fs.Files.Get(name);

        return new FileMeta(_fs, pos);
    }

    public List<(IParentedKey name, uint ino)> ListSubDir()
    {
        var selfInfo = GetInfo();
        uint index = selfInfo.GetSubDir();
        var result = new List<(IParentedKey, uint)>();

        while (index != 0)
        {
            var (infoObj, key) = _fs.Dirs.GetAt(index);

            result.Add((key, index));
            index = ((IDirInfo)infoObj).GetNext();
        }

        return result;
    }

    public List<(IParentedKey name, uint ino)> ListSubFile()
    {
        var selfInfo = GetInfo();
        uint index = selfInfo.GetSubFile();
        var result = new List<(IParentedKey, uint)>();

        while (index != 0)
        {
            var (infoObj, key) = _fs.Files.GetAt(index);

            result.Add((key, index));
            index = ((IFileInfo)infoObj).GetNext();
        }

        return result;
    }

    public DirMeta NewSubDir(IParentedKey key, IDirInfo info) => NewSubDirImpl(key, info, resetSubInfo: true);

    public DirMeta NewSubDirImpl(IParentedKey key, IDirInfo info, bool resetSubInfo)
    {
        var selfInfo = GetInfo();

        info.SetNext(selfInfo.GetSubDir());

        if (resetSubInfo)
        {
            info.SetSubDir(0);
            info.SetSubFile(0);
        }

        uint pos = _fs.Dirs.Add(key, info);

        selfInfo.SetSubDir(pos);
        _fs.Dirs.SetDirInfo(_ino, selfInfo);

        return new DirMeta(_fs, pos);
    }

    public FileMeta NewSubFile(IParentedKey key, IFileInfo info)
    {
        var selfInfo = GetInfo();

        info.SetNext(selfInfo.GetSubFile());

        uint pos = _fs.Files.Add(key, info);

        selfInfo.SetSubFile(pos);

        _fs.Dirs.SetDirInfo(_ino, selfInfo);

        return new FileMeta(_fs, pos);
    }

    public void Delete()
    {
        CheckExclusive();

        var selfInfo = GetInfo();

        if (_ino == 1)
            throw new InvalidOperationException("DirMeta::delete: cannot delete root");

        if (selfInfo.GetSubDir() != 0 || selfInfo.GetSubFile() != 0)
            throw new InvalidOperationException("DirMeta::delete: not empty");

        DeleteImpl();
    }

    public void DeleteImpl()
    {
        var selfInfo = GetInfo();
        uint parentIndex = GetParentIno();
        var (parentInfoObj, _) = _fs.Dirs.GetAt(parentIndex);
        var parentInfo = (IDirInfo)parentInfoObj;
        uint headIndex = parentInfo.GetSubDir();

        if (headIndex == _ino)
        {
            parentInfo.SetSubDir(selfInfo.GetNext());
            _fs.Dirs.SetDirInfo(parentIndex, parentInfo);
        }
        else
        {
            while (true)
            {
                var (headInfoObj, _) = _fs.Dirs.GetAt(headIndex);
                var headInfo = (IDirInfo)headInfoObj;
                uint nextIndex = headInfo.GetNext();

                if (nextIndex == _ino)
                {
                    headInfo.SetNext(selfInfo.GetNext());
                    _fs.Dirs.SetDirInfo(headIndex, headInfo);

                    break;
                }

                headIndex = nextIndex;
            }
        }

        _fs.Dirs.Remove(_ino);
    }

    public void Rename(DirMeta parent, IParentedKey newKey)
    {
        var info = GetInfo();

        DeleteImpl();

        var newDir = parent.NewSubDirImpl(newKey, info, resetSubInfo: false);

        _fs.Dirs.ReleaseTicket(_ino);

        _ino = newDir.Ino;
        newDir._disposed = true;
    }
}