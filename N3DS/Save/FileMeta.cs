using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class FileMeta : IDisposable
{
    private uint _ino;
    private readonly FsMeta _fs;
    private bool _disposed;

    public uint Ino => _ino;

    public FileMeta(FsMeta fs, uint ino)
    {
        _fs = fs;
        _ino = ino;

        _fs.Files.AcquireTicket(ino);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fs.Files.ReleaseTicket(_ino);

            _disposed = true;
        }
    }

    public void CheckExclusive() => _fs.Files.CheckExclusive(_ino);

    public uint GetParentIno()
    {
        var (_, key) = _fs.Files.GetAt(_ino);

        return key.GetParent();
    }

    public IFileInfo GetInfo() => (IFileInfo)_fs.Files.GetAt(_ino).info;

    public void SetInfo(IFileInfo info) => _fs.Files.SetFileInfo(_ino, info);

    public void Delete()
    {
        CheckExclusive();
        DeleteImpl();
    }

    public void DeleteImpl()
    {
        var selfInfo = GetInfo();
        uint parentIndex = GetParentIno();
        var (parentInfoObj, _) = _fs.Dirs.GetAt(parentIndex);
        var parentInfo = (IDirInfo)parentInfoObj;
        uint headIndex = parentInfo.GetSubFile();

        if (headIndex == _ino)
        {
            parentInfo.SetSubFile(selfInfo.GetNext());
            _fs.Dirs.SetDirInfo(parentIndex, parentInfo);
        }
        else
        {
            while (true)
            {
                var (headInfoObj, _) = _fs.Files.GetAt(headIndex);
                var headInfo = (IFileInfo)headInfoObj;
                uint nextIndex = headInfo.GetNext();

                if (nextIndex == _ino)
                {
                    headInfo.SetNext(selfInfo.GetNext());
                    _fs.Files.SetFileInfo(headIndex, headInfo);

                    break;
                }
                headIndex = nextIndex;
            }
        }
        _fs.Files.Remove(_ino);
    }

    public void Rename(DirMeta parent, IParentedKey newKeyTemplate)
    {
        var info = GetInfo();

        DeleteImpl();

        var newFile = parent.NewSubFile(newKeyTemplate, info);

        _fs.Files.ReleaseTicket(_ino);

        _ino = newFile.Ino;
        newFile._disposed = true;
    }
}