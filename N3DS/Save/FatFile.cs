using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class FatFile(Fat fat, List<BlockMap> blockList) : IRandomAccessFile
{
    public readonly List<BlockMap> BlockList = blockList;

    public int Length => BlockList.Count * fat.BlockLen;

    public void Read(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > Length)
            throw new InvalidOperationException("FatFile::read out of bound");

        int beginBlock = pos / fat.BlockLen;
        int endBlock = Misc.DivideUp(end, fat.BlockLen);

        for (int i = beginBlock; i < endBlock; i++)
        {
            int dataBeginAsBlock = i * fat.BlockLen;
            int dataEndAsBlock = (i + 1) * fat.BlockLen;
            int dataBegin = Math.Max(dataBeginAsBlock, pos);
            int dataEnd = Math.Min(dataEndAsBlock, end);
            int blockIndex = BlockList[i].BlockIndex;
            int blockOffset = blockIndex * fat.BlockLen + (dataBegin - dataBeginAsBlock);

            fat.Data.Read(blockOffset, buf, offset + (dataBegin - pos), dataEnd - dataBegin);
        }
    }

    public void Write(int pos, byte[] buf, int offset, int count)
    {
        int end = pos + count;

        if (end > Length)
            throw new InvalidOperationException("FatFile::write out of bound");

        int beginBlock = pos / fat.BlockLen;
        int endBlock = Misc.DivideUp(end, fat.BlockLen);

        for (int i = beginBlock; i < endBlock; i++)
        {
            int dataBeginAsBlock = i * fat.BlockLen;
            int dataEndAsBlock = (i + 1) * fat.BlockLen;
            int dataBegin = Math.Max(dataBeginAsBlock, pos);
            int dataEnd = Math.Min(dataEndAsBlock, end);
            int blockIndex = BlockList[i].BlockIndex;
            int blockOffset = blockIndex * fat.BlockLen + (dataBegin - dataBeginAsBlock);

            fat.Data.Write(blockOffset, buf, offset + (dataBegin - pos), dataEnd - dataBegin);
        }
    }

    public void Commit() { }

    public void Delete() => fat.Delete(this);

    public void Resize(int blockCount) => fat.Resize(this, blockCount);
}