using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class DirMeta : IDisposable
{
    private readonly uint _ino;
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

            GC.SuppressFinalize(this);
        }
    }

    public uint GetParentIno()
    {
        var (_, key) = _fs.Dirs.GetAt(_ino);
        return key.GetParent();
    }

    public IDirInfo GetInfo() => (IDirInfo)_fs.Dirs.GetAt(_ino).info;

    public FileMeta OpenSubFile(IParentedKey name)
    {
        var (_, pos) = _fs.Files.Get(name);
        return new FileMeta(_fs, pos);
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

    public FileMeta NewSubFile(IParentedKey key, IFileInfo info)
    {
        var selfInfo = GetInfo();

        info.SetNext(selfInfo.GetSubFile());

        uint pos = _fs.Files.Add(key, info);

        selfInfo.SetSubFile(pos);

        _fs.Dirs.SetDirInfo(_ino, selfInfo);

        return new FileMeta(_fs, pos);
    }
}