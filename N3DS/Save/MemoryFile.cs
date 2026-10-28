using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class MemoryFile(byte[] data) : IRandomAccessFile
{
    public int Length => data.Length;

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > data.Length)
            throw new InvalidOperationException("MemoryFile::read out of bound");

        Buffer.BlockCopy(data, pos, buf, offset, count);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > data.Length)
            throw new InvalidOperationException("MemoryFile::write out of bound");

        Buffer.BlockCopy(buf, offset, data, pos, count);
    }

    public void Commit() { }
}