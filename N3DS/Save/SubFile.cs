using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class SubFile : IRandomAccessFile
{
    private readonly IRandomAccessFile _parent;
    private readonly int _begin;
    private readonly int _len;

    public int Length => _len;

    public SubFile(IRandomAccessFile parent, int begin, int len)
    {
        if (begin + len > parent.Length)
            throw new InvalidOperationException("SubFile::new out of bound");

        _parent = parent;
        _begin = begin;
        _len = len;
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("SubFile::read out of bound");

        _parent.Read(pos + _begin, buf, offset, count);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("SubFile::write out of bound");

        _parent.Write(pos + _begin, buf, offset, count);
    }

    public void Commit() { }
}