using N3DS.Save.Interfaces;

namespace N3DS.Save;

public class DualFile : IRandomAccessFile
{
    private readonly IRandomAccessFile _selector;
    private readonly IRandomAccessFile[] _pair;
    private byte _modified;
    private readonly int _len;

    public int Length => _len;

    public DualFile(IRandomAccessFile selector, IRandomAccessFile[] pair)
    {
        if (pair.Length != 2)
            throw new InvalidOperationException("DualFile::new pair must have exactly 2 elements");

        if (pair[0].Length != pair[1].Length)
            throw new InvalidOperationException("DualFile::new size mismatch");

        if (selector.Length != 1)
            throw new InvalidOperationException("DualFile::new selector size mismatch");

        _selector = selector;
        _pair = pair;
        _modified = 0;
        _len = pair[0].Length;
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        if (pos + count > _len)
            throw new InvalidOperationException("DualFile::read out of bound");

        var selectBuf = new byte[1];

        _selector.Read(0, selectBuf, 0, 1);

        int select = (selectBuf[0] ^ _modified) & 1;

        _pair[select].Read(pos, buf, offset, count);
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > _len)
            throw new InvalidOperationException("DualFile::write out of bound");

        var selectBuf = new byte[1];

        _selector.Read(0, selectBuf, 0, 1);

        int prev = selectBuf[0] & 1;
        int cur = 1 - prev;

        _pair[cur].Write(pos, buf, offset, count);

        if (_modified == 0)
        {
            if (pos != 0)
            {
                var edgeBuf = new byte[pos];

                _pair[prev].Read(0, edgeBuf, 0, pos);
                _pair[cur].Write(0, edgeBuf, 0, pos);
            }

            if (end != _len)
            {
                var edgeBuf = new byte[_len - end];

                _pair[prev].Read(end, edgeBuf, 0, _len - end);
                _pair[cur].Write(end, edgeBuf, 0, _len - end);
            }
            _modified = 1;
        }
    }

    public void Commit()
    {
        if (_modified == 1)
        {
            var selectBuf = new byte[1];

            _selector.Read(0, selectBuf, 0, 1);

            selectBuf[0] = (byte)(1 - selectBuf[0]);

            _selector.Write(0, selectBuf, 0, 1);

            _modified = 0;
        }
    }
}