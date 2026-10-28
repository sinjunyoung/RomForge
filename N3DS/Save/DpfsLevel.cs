using N3DS.Save.Interfaces;
using System.Buffers.Binary;

namespace N3DS.Save;

public class DpfsLevel : IRandomAccessFile
{
    private readonly IRandomAccessFile _selector;
    private readonly IRandomAccessFile[] _pair;
    private readonly int _blockLen;
    private readonly int _len;
    private readonly uint[] _dirty;
    private readonly byte[] _blockBuf;

    public int Length => _len;

    public DpfsLevel(IRandomAccessFile selector, IRandomAccessFile[] pair, int blockLen)
    {
        if (pair.Length != 2)
            throw new InvalidOperationException("DpfsLevel::new pair must have exactly 2 elements");

        int len = pair[0].Length;

        if (pair[1].Length != len)
            throw new InvalidOperationException("DpfsLevel::new size mismatch");

        int blockCount = Misc.DivideUp(len, blockLen);
        int chunkCount = Misc.DivideUp(blockCount, 32);

        if (chunkCount * 4 > selector.Length)
            throw new InvalidOperationException("DpfsLevel::new selector size mismatch");

        _selector = selector;
        _pair = pair;
        _blockLen = blockLen;
        _blockBuf = new byte[_blockLen];
        _len = len;
        _dirty = new uint[chunkCount];
    }

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > _len)
            throw new InvalidOperationException("DpfsLevel::read out of bound");

        int beginBlock = pos / _blockLen;
        int endBlock = Misc.DivideUp(end, _blockLen);
        int beginChunk = beginBlock / 32;
        int endChunk = Misc.DivideUp(endBlock, 32);
        var selectorBuf = new byte[(endChunk - beginChunk) * 4];

        _selector.Read(beginChunk * 4, selectorBuf, 0, selectorBuf.Length);

        for (int chunkI = beginChunk; chunkI < endChunk; chunkI++)
        {
            uint dirty = _dirty[chunkI];
            int rawOffset = (chunkI - beginChunk) * 4;
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(selectorBuf.AsSpan(rawOffset, 4));
            uint select = dirty ^ raw;
            int blockIBegin = Math.Max(chunkI * 32, beginBlock);
            int blockIEnd = Math.Min((chunkI + 1) * 32, endBlock);

            for (int blockI = blockIBegin; blockI < blockIEnd; blockI++)
            {
                int shift = 31 - (blockI - chunkI * 32);
                int selectBit = (int)((select >> shift) & 1);
                int dataBegin = Math.Max(blockI * _blockLen, pos);
                int dataEnd = Math.Min((blockI + 1) * _blockLen, end);

                _pair[selectBit].Read(dataBegin, buf, offset + (dataBegin - pos), dataEnd - dataBegin);
            }
        }
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > _len)
            throw new InvalidOperationException("DpfsLevel::write out of bound");

        int beginBlock = pos / _blockLen;
        int endBlock = Misc.DivideUp(end, _blockLen);
        int beginChunk = beginBlock / 32;
        int endChunk = Misc.DivideUp(endBlock, 32);
        var selectorBuf = new byte[(endChunk - beginChunk) * 4];

        _selector.Read(beginChunk * 4, selectorBuf, 0, selectorBuf.Length);

        for (int chunkI = beginChunk; chunkI < endChunk; chunkI++)
        {
            int rawOffset = (chunkI - beginChunk) * 4;
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(selectorBuf.AsSpan(rawOffset, 4));
            uint select = ~raw;
            int blockIBegin = Math.Max(chunkI * 32, beginBlock);
            int blockIEnd = Math.Min((chunkI + 1) * 32, endBlock);

            for (int blockI = blockIBegin; blockI < blockIEnd; blockI++)
            {
                int shift = 31 - (blockI - chunkI * 32);
                int selectBit = (int)((select >> shift) & 1);
                int dataBeginAsBlock = blockI * _blockLen;
                int dataEndAsBlock = Math.Min((blockI + 1) * _blockLen, _len);
                int dataBegin = Math.Max(dataBeginAsBlock, pos);
                int dataEnd = Math.Min(dataEndAsBlock, end);

                _pair[selectBit].Write(dataBegin, buf, offset + (dataBegin - pos), dataEnd - dataBegin);

                uint keepBit = (_dirty[chunkI] >> shift) & 1;

                if (keepBit == 0)
                {
                    int other = 1 - selectBit;

                    if (dataBegin > dataBeginAsBlock)
                    {
                        int marginLen = dataBegin - dataBeginAsBlock;

                        _pair[other].Read(dataBeginAsBlock, _blockBuf, 0, marginLen);
                        _pair[selectBit].Write(dataBeginAsBlock, _blockBuf, 0, marginLen);
                    }

                    if (dataEnd < dataEndAsBlock)
                    {
                        int marginLen = dataEndAsBlock - dataEnd;

                        _pair[other].Read(dataEnd, _blockBuf, 0, marginLen);
                        _pair[selectBit].Write(dataEnd, _blockBuf, 0, marginLen);
                    }
                }

                _dirty[chunkI] |= (uint)(1 << shift);
            }
        }
    }

    public void Commit()
    {
        var bytes = new byte[4];

        for (int i = 0; i < _dirty.Length; i++)
        {
            if (_dirty[i] != 0)
            {
                _selector.Read(i * 4, bytes, 0, 4);

                uint oldWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                uint newWord = oldWord ^ _dirty[i];

                BinaryPrimitives.WriteUInt32LittleEndian(bytes, newWord);
                _selector.Write(i * 4, bytes, 0, 4);

                _dirty[i] = 0;
            }
        }
    }
}