using N3DS.Save.Interfaces;
using System.Security.Cryptography;

namespace N3DS.Save;

public class IvfcLevel : IRandomAccessFile
{
    private const byte BlockUnverified = 0;
    private const byte BlockVerified = 1;
    private const byte BlockModified = 2;
    private const byte BlockBroken = 3;
    private readonly IRandomAccessFile _hash;
    private readonly IRandomAccessFile _data;
    private readonly int _blockLen;
    private readonly int _len;
    private readonly byte[] _status;

    public int Length => _len;

    public IvfcLevel(IRandomAccessFile hash, IRandomAccessFile data, int blockLen)
    {
        int len = data.Length;
        int blockCount = Misc.DivideUp(len, blockLen);

        if (blockCount * 0x20 > hash.Length)
            throw new InvalidOperationException("IvfcLevel::new size mismatch");

        int chunkCount = Misc.DivideUp(blockCount, 4);

        _hash = hash;
        _data = data;
        _blockLen = blockLen;
        _len = len;
        _status = new byte[chunkCount];
    }

    private byte GetStatus(int blockIndex) => (byte)((_status[blockIndex / 4] >> ((blockIndex % 4) * 2)) & 3);

    private void SetStatus(int blockIndex, byte status)
    {
        int i = blockIndex / 4;
        int j = (blockIndex % 4) * 2;

        _status[i] &= (byte)~(3 << j);
        _status[i] |= (byte)(status << j);
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > _len)
            throw new InvalidOperationException("IvfcLevel::read out of bound");

        bool anyBroken = false;
        int beginBlock = pos / _blockLen;
        int endBlock = Misc.DivideUp(end, _blockLen);

        for (int i = beginBlock; i < endBlock; i++)
        {
            int dataBeginAsBlock = i * _blockLen;
            int dataEndAsBlock = Math.Min((i + 1) * _blockLen, _len);
            int dataBegin = Math.Max(dataBeginAsBlock, pos);
            int dataEnd = Math.Min(dataEndAsBlock, end);
            int destOffset = offset + (dataBegin - pos);
            int destCount = dataEnd - dataBegin;
            byte status = GetStatus(i);

            if (status == BlockBroken)
            {
                anyBroken = true;
                buf.AsSpan(destOffset, destCount).Fill(0xDD);
            }
            else if (status == BlockVerified || status == BlockModified)
                _data.Read(dataBegin, buf, destOffset, destCount);
            else
            {
                var blockBuf = new byte[_blockLen];
                int blockDataLen = dataEndAsBlock - dataBeginAsBlock;

                _data.Read(dataBeginAsBlock, blockBuf, 0, blockDataLen);

                var hashStored = new byte[0x20];
                bool hashReadOk;

                try
                {
                    _hash.Read(i * 0x20, hashStored, 0, 0x20);

                    hashReadOk = true;
                }
                catch
                {
                    hashReadOk = false;
                }

                if (!hashReadOk)
                {
                    SetStatus(i, BlockBroken);

                    anyBroken = true;

                    buf.AsSpan(destOffset, destCount).Fill(0xDD);

                    continue;
                }

                byte[] hashComputed = SHA256.HashData(blockBuf);

                if (hashComputed.AsSpan().SequenceEqual(hashStored))
                {
                    SetStatus(i, BlockVerified);
                    Buffer.BlockCopy(blockBuf, dataBegin - dataBeginAsBlock, buf, destOffset, destCount);
                }
                else
                {
                    SetStatus(i, BlockBroken);

                    anyBroken = true;

                    buf.AsSpan(destOffset, destCount).Fill(0xDD);
                }
            }
        }

        if (anyBroken)
            throw new InvalidDataException("IvfcLevel::read hash mismatch");
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > _len)
            throw new InvalidOperationException("IvfcLevel::write out of bound");

        _data.Write(pos, buf, offset, count);

        int beginBlock = pos / _blockLen;
        int endBlock = Misc.DivideUp(end, _blockLen);

        for (int i = beginBlock; i < endBlock; i++)
            SetStatus(i, BlockModified);
    }

    public void Commit()
    {
        int blockCount = Misc.DivideUp(_len, _blockLen);
        var blockBuf = new byte[_blockLen];

        for (int i = 0; i < blockCount; i++)
        {
            if (GetStatus(i) == BlockModified)
            {
                int begin = i * _blockLen;
                int end = Math.Min((i + 1) * _blockLen, _len);
                int blockDataLen = end - begin;

                Array.Clear(blockBuf, 0, _blockLen);
                _data.Read(begin, blockBuf, 0, blockDataLen);

                byte[] hash = SHA256.HashData(blockBuf);

                _hash.Write(i * 0x20, hash, 0, 0x20);
                SetStatus(i, BlockVerified);
            }
        }
    }
}