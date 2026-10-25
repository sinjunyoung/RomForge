using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class Fat
{
    public readonly IRandomAccessFile Table;
    public readonly IRandomAccessFile Data;
    public readonly int BlockLen;
    public int FreeBlocks;

    private static int? IndexBadToGood(uint index) => index == 0 ? null : (int?)(index - 1);

    private static uint IndexGoodToBad(int? index) => index.HasValue ? (uint)(index.Value + 1) : 0u;

    private static FatNode GetNode(IRandomAccessFile table, int index)
    {
        var nodeStart = Entry.Read(table, (index + 1) * Entry.Size);
        bool flagSet = nodeStart.U.Flag == 1;
        bool indexZero = nodeStart.U.Index == 0;

        if (flagSet != indexZero)
            throw new InvalidDataException("Fat::get_node: broken entry");

        int size;

        if (nodeStart.V.Flag == 1)
        {
            int startI = index + 2;
            var expandStart = Entry.Read(table, startI * Entry.Size);

            if (expandStart.U.Flag == 0 || expandStart.V.Flag == 1 || expandStart.U.Index != (uint)(index + 1))
                throw new InvalidDataException("Fat::get_node: expanded node broken starting entry");

            int endI = (int)expandStart.V.Index;
            var expandEnd = Entry.Read(table, endI * Entry.Size);

            if (!expandStart.Equals(expandEnd))
                throw new InvalidDataException("Fat::get_node: expanded node broken end entry");

            size = (int)(expandStart.V.Index - expandStart.U.Index + 1);
        }
        else
            size = 1;

        return new FatNode
        {
            Size = size,
            Prev = IndexBadToGood(nodeStart.U.Index),
            Next = IndexBadToGood(nodeStart.V.Index),
        };
    }

    private static void SetNode(IRandomAccessFile table, int index, FatNode node)
    {
        var nodeStart = new Entry
        {
            U = new EntryHalf { Flag = node.Prev.HasValue ? 0u : 1u, Index = IndexGoodToBad(node.Prev) },
            V = new EntryHalf { Flag = node.Size != 1 ? 1u : 0u, Index = IndexGoodToBad(node.Next) },
        };

        nodeStart.Write(table, (index + 1) * Entry.Size);

        if (node.Size != 1)
        {
            var expand = new Entry
            {
                U = new EntryHalf { Flag = 1, Index = IndexGoodToBad(index) },
                V = new EntryHalf { Flag = 0, Index = IndexGoodToBad(index + node.Size - 1) },
            };

            expand.Write(table, (index + 2) * Entry.Size);
            expand.Write(table, (index + node.Size) * Entry.Size);
        }
    }

    private static int? GetHead(IRandomAccessFile table)
    {
        var head = Entry.Read(table, 0);

        if (head.U.Index != 0 || head.U.Flag != 0 || head.V.Flag != 0)
            throw new InvalidDataException("Fat::get_head: broken head");

        return IndexBadToGood(head.V.Index);
    }

    private static void SetHead(IRandomAccessFile table, int? index)
    {
        var head = new Entry
        {
            U = new EntryHalf { Flag = 0, Index = 0 },
            V = new EntryHalf { Flag = 0, Index = IndexGoodToBad(index) },
        };

        head.Write(table, 0);
    }

    private static List<BlockMap> Allocate(IRandomAccessFile table, int blockCount)
    {
        var blockList = new List<BlockMap>(blockCount);
        int cur = GetHead(table)!.Value;

        while (true)
        {
            var node = GetNode(table, cur);

            if (node.Size <= blockCount)
            {
                for (int i = cur; i < cur + node.Size; i++)
                    blockList.Add(new BlockMap { BlockIndex = i, NodeStartIndex = cur });

                blockCount -= node.Size;

                if (blockCount == 0)
                {
                    if (node.Next.HasValue)
                    {
                        var nextNode = GetNode(table, node.Next.Value);

                        nextNode.Prev = null;

                        SetNode(table, node.Next.Value, nextNode);
                    }

                    SetHead(table, node.Next);

                    node.Next = null;

                    SetNode(table, cur, node);

                    break;
                }

                cur = node.Next!.Value;
            }
            else
            {
                var left = new FatNode { Size = blockCount, Prev = node.Prev, Next = null };
                var right = new FatNode { Size = node.Size - blockCount, Prev = null, Next = node.Next };

                SetNode(table, cur, left);
                SetNode(table, cur + blockCount, right);

                if (node.Next.HasValue)
                {
                    var nextNode = GetNode(table, node.Next.Value);

                    if (nextNode.Prev != cur)
                        throw new InvalidDataException("Fat::allocate: inconsistent prev pointer");

                    nextNode.Prev = cur + blockCount;

                    SetNode(table, node.Next.Value, nextNode);
                }

                SetHead(table, cur + blockCount);

                for (int i = cur; i < cur + blockCount; i++)
                    blockList.Add(new BlockMap { BlockIndex = i, NodeStartIndex = cur });

                break;
            }
        }

        return blockList;
    }

    private static void Free(IRandomAccessFile table, List<BlockMap> blockList)
    {
        int lastNodeIndex = blockList[^1].NodeStartIndex;
        int? maybeFreeIndex = GetHead(table);

        if (maybeFreeIndex.HasValue)
        {
            var freeFront = GetNode(table, maybeFreeIndex.Value);

            if (freeFront.Prev.HasValue)
                throw new InvalidDataException("Fat::free: trying to free from middle");

            freeFront.Prev = lastNodeIndex;

            SetNode(table, maybeFreeIndex.Value, freeFront);
        }

        var lastNode = GetNode(table, lastNodeIndex);

        if (lastNode.Next.HasValue)
            throw new InvalidDataException("Fat::free: block list ends too early");

        lastNode.Next = maybeFreeIndex;

        SetNode(table, lastNodeIndex, lastNode);
        SetHead(table, blockList[0].BlockIndex);
    }

    private static void IterateFatEntry(IRandomAccessFile table, int firstEntry, Action<int, int> callback)
    {
        int? cur = firstEntry;
        int? prev = null;

        while (cur.HasValue)
        {
            var node = GetNode(table, cur.Value);

            if (node.Prev != prev)
                throw new InvalidDataException("Fat::iterate: inconsistent prev pointer");

            callback(cur.Value, node.Size);
            prev = cur;
            cur = node.Next;
        }
    }

    public static void Format(IRandomAccessFile table)
    {
        int blockCount = table.Length / 8 - 1;

        SetHead(table, 0);
        SetNode(table, 0, new FatNode { Size = blockCount, Prev = null, Next = null });
    }

    public Fat(IRandomAccessFile table, IRandomAccessFile data, int blockLen)
    {
        int tableLen = table.Length;
        int dataLen = data.Length;

        if (tableLen % 8 != 0)
            throw new InvalidOperationException("Fat::new: table size not multiple of 8");

        int blockCount = tableLen / 8 - 1;

        if (dataLen != blockCount * blockLen)
            throw new InvalidOperationException("Fat::new: data size mismatch");

        int freeBlocks = 0;
        int? head = GetHead(table);

        if (head.HasValue)
            IterateFatEntry(table, head.Value, (_, nodeSize) => freeBlocks += nodeSize);

        Table = table;
        Data = data;
        BlockLen = blockLen;
        FreeBlocks = freeBlocks;
    }

    public FatFile Open(int firstBlock)
    {
        var blockList = new List<BlockMap>();

        IterateFatEntry(Table, firstBlock, (nodeStart, nodeSize) =>
        {
            for (int i = 0; i < nodeSize; i++)
                blockList.Add(new BlockMap { BlockIndex = nodeStart + i, NodeStartIndex = nodeStart });
        });

        return new FatFile(this, blockList);
    }

    public (FatFile file, int firstBlock) Create(int blockCount)
    {
        if (blockCount == 0)
            throw new InvalidOperationException("Fat::create: block count must be > 0");

        if (FreeBlocks < blockCount)
            throw new InvalidOperationException("Fat::create: no space");

        FreeBlocks -= blockCount;

        var blockList = Allocate(Table, blockCount);
        int first = blockList[0].BlockIndex;

        return (new FatFile(this, blockList), first);
    }

    public void Delete(FatFile file)
    {
        Free(Table, file.BlockList);

        FreeBlocks += file.BlockList.Count;
    }

    public void Resize(FatFile file, int blockCount)
    {
        if (blockCount == 0)
            throw new InvalidOperationException("Fat::resize: block count must be > 0");

        if (blockCount == file.BlockList.Count)
            return;

        if (blockCount > file.BlockList.Count)
        {
            int delta = blockCount - file.BlockList.Count;

            if (FreeBlocks < delta)
                throw new InvalidOperationException("Fat::resize: no space");

            var extra = Allocate(Table, delta);
            int tailIndex = file.BlockList[^1].NodeStartIndex;
            int headIndex = extra[0].BlockIndex;
            var tail = GetNode(Table, tailIndex);

            tail.Next = headIndex;

            SetNode(Table, tailIndex, tail);

            var head = GetNode(Table, headIndex);

            head.Prev = tailIndex;

            SetNode(Table, headIndex, head);
            file.BlockList.AddRange(extra);

            FreeBlocks -= delta;
        }
        else
        {
            int delta = file.BlockList.Count - blockCount;
            var splitHead = file.BlockList[blockCount];
            int splitHeadIndex = splitHead.BlockIndex;

            if (splitHeadIndex == splitHead.NodeStartIndex)
            {
                int tailIndex = file.BlockList[blockCount - 1].NodeStartIndex;
                var tail = GetNode(Table, tailIndex);

                tail.Next = null;

                SetNode(Table, tailIndex, tail);

                var head2 = GetNode(Table, splitHeadIndex);

                head2.Prev = null;

                SetNode(Table, splitHeadIndex, head2);
            }
            else
            {
                int tailIndex = splitHead.NodeStartIndex;

                for (int i = blockCount; i < file.BlockList.Count; i++)
                {
                    if (file.BlockList[i].NodeStartIndex == tailIndex)
                        file.BlockList[i] = new BlockMap { BlockIndex = file.BlockList[i].BlockIndex, NodeStartIndex = splitHeadIndex };
                    else
                        break;
                }

                var tailNode = GetNode(Table, tailIndex);
                int tailSize = tailNode.Size;

                tailNode.Size = splitHeadIndex - tailIndex;

                int? next = tailNode.Next;
                tailNode.Next = null;

                SetNode(Table, tailIndex, tailNode);
                SetNode(Table, splitHeadIndex, new FatNode
                {
                    Prev = null,
                    Next = next,
                    Size = tailSize - (splitHeadIndex - tailIndex),
                });

                if (next.HasValue)
                {
                    var nextNode = GetNode(Table, next.Value);

                    if (nextNode.Prev != tailIndex)
                        throw new InvalidDataException("Fat::resize: inconsistent prev pointer");

                    nextNode.Prev = splitHeadIndex;

                    SetNode(Table, next.Value, nextNode);
                }
            }

            Free(Table, file.BlockList.GetRange(blockCount, delta));
            file.BlockList.RemoveRange(blockCount, delta);

            FreeBlocks += delta;
        }
    }
}