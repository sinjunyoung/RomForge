using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class DiskFile : IRandomAccessFile, IDisposable
{
    private readonly FileStream _stream;
    private readonly int _len;

    public int Length => _len;

    public DiskFile(string path, bool readOnly = false)
    {
        _stream = new FileStream(path, FileMode.Open, readOnly ? FileAccess.Read : FileAccess.ReadWrite, FileShare.Read);
        _len = (int)_stream.Length;
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("DiskFile::read out of bound");

        _stream.Seek(pos, SeekOrigin.Begin);
        _stream.ReadExactly(buf, offset, count);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("DiskFile::write out of bound");

        _stream.Seek(pos, SeekOrigin.Begin);
        _stream.Write(buf, offset, count);
    }

    public void Commit() => _stream.Flush();

    public void Dispose() => _stream.Dispose();
}