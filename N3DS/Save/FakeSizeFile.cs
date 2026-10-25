using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class FakeSizeFile(IRandomAccessFile parent, int len) : IRandomAccessFile
{
    public int Length => len;

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos >= parent.Length) 
            return;

        int end = Math.Min(pos + count, parent.Length);

        parent.Read(pos, buf, offset, end - pos);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        if (pos >= parent.Length)
            return;

        int end = Math.Min(pos + count, parent.Length);

        parent.Write(pos, buf, offset, end - pos);
    }

    public void Commit() { }
}