using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

public sealed class Xd3FgkDecoder
{
    private const uint AlphabetSize = 256;
    private FgkStream? _state;

    public byte[] Decode(byte[] input, ref int inputPos, int inputEnd, int outputLen)
    {
        if (_state == null)
        {
            _state = Alloc();
            Init(_state);
        }

        FgkStream h = _state;
        var output = new byte[outputLen];
        int outPos = 0;

        while (true)
        {
            if (inputPos == inputEnd)
                throw new Xd3Exception("secondary decoder end of input");

            byte curByte = input[inputPos++];

            for (uint curMask = 1; curMask != 0x100; curMask <<= 1)
            {
                bool done = DecodeBit(h, (curByte & curMask) != 0 ? 1u : 0u);

                if (!done)
                    continue;

                output[outPos++] = (byte)DecodeData(h);

                if (outPos == outputLen)
                    return output;
            }
        }
    }

    private static FgkStream Alloc()
    {
        var h = new FgkStream { AlphabetSize = AlphabetSize };
        uint totalNodes = 2 * AlphabetSize - 1;

        h.Alphabet = new FgkNode[totalNodes];

        for (int i = 0; i < totalNodes; i++)
            h.Alphabet[i] = new FgkNode();

        h.CodedBits = new byte[AlphabetSize];

        return h;
    }

    private static void Init(FgkStream h)
    {
        h.RootNode = h.Alphabet[0];
        h.DecodePtr = h.RootNode;
        h.FreeNodeIndex = (int)h.AlphabetSize;
        h.RemainingZeros = h.Alphabet[0];
        h.CodedDepth = 0;
        h.ZeroFreqCount = h.AlphabetSize + 2;

        FactorRemaining(h);
        FactorRemaining(h);

        for (long si = (long)h.AlphabetSize - 1; si >= 0; si--)
            InitNode(h.Alphabet[si], (uint)si, h.AlphabetSize, h.Alphabet);
    }

    private static void InitNode(FgkNode node, uint i, uint size, FgkNode[] alphabet)
    {
        node.Index = (int)i;
        node.RightChild = i < size - 1 ? alphabet[i + 1] : null;
        node.LeftChild = i >= 1 ? alphabet[i - 1] : null;
        node.Weight = 0;
        node.Parent = null;
        node.Right = null;
        node.Left = null;
        node.MyBlock = null;
    }

    private static void FactorRemaining(FgkStream h)
    {
        h.ZeroFreqCount -= 1;

        uint i = h.ZeroFreqCount;

        h.ZeroFreqExp = 0;

        while (i > 1)
        {
            h.ZeroFreqExp += 1;
            i >>= 1;
        }

        i = 1u << (int)h.ZeroFreqExp;
        h.ZeroFreqRem = h.ZeroFreqCount - i;
    }

    private static FgkBlock MakeBlock(FgkNode lead) => new () { Leader = lead };

    private static void FreeBlock(FgkBlock b) { }

    private static void EliminateZero(FgkStream h, FgkNode node)
    {
        if (h.ZeroFreqCount == 1)
            return;

        FactorRemaining(h);

        if (node.LeftChild == null)
        {
            h.RemainingZeros = h.RemainingZeros!.RightChild;
            h.RemainingZeros!.LeftChild = null;
        }
        else if (node.RightChild == null)
            node.LeftChild.RightChild = null;
        else
        {
            node.RightChild.LeftChild = node.LeftChild;
            node.LeftChild.RightChild = node.RightChild;
        }
    }

    private static FgkNode IncreaseZeroWeight(FgkStream h, uint n)
    {
        FgkNode thisZero = h.Alphabet[n];

        if (h.ZeroFreqCount == 1)
        {
            thisZero.RightChild = null;

            if (thisZero.Right!.Weight == 1)
                thisZero.MyBlock = thisZero.Right.MyBlock;
            else
                thisZero.MyBlock = MakeBlock(thisZero);

            h.RemainingZeros = null;

            return thisZero;
        }

        FgkNode zeroPtr = h.RemainingZeros!;
        FgkNode newInternal = h.Alphabet[h.FreeNodeIndex++];

        newInternal.Parent = zeroPtr.Parent;
        newInternal.Right = zeroPtr.Right;
        newInternal.Weight = 0;
        newInternal.RightChild = thisZero;
        newInternal.Left = thisZero;

        if (h.RemainingZeros == h.RootNode)
        {
            h.RootNode = newInternal;
            thisZero.MyBlock = MakeBlock(thisZero);
            newInternal.MyBlock = MakeBlock(newInternal);
        }
        else
        {
            newInternal.Right!.Left = newInternal;

            if (zeroPtr.Parent!.RightChild == zeroPtr)
                zeroPtr.Parent.RightChild = newInternal;
            else
                zeroPtr.Parent.LeftChild = newInternal;

            if (newInternal.Right.Weight == 1)
                newInternal.MyBlock = newInternal.Right.MyBlock;
            else
                newInternal.MyBlock = MakeBlock(newInternal);

            thisZero.MyBlock = newInternal.MyBlock;
        }

        EliminateZero(h, thisZero);

        newInternal.LeftChild = h.RemainingZeros;
        thisZero.Right = newInternal;
        thisZero.Left = h.RemainingZeros;
        thisZero.Parent = newInternal;
        thisZero.LeftChild = null;
        thisZero.RightChild = null;
        h.RemainingZeros!.Parent = newInternal;
        h.RemainingZeros.Right = thisZero;

        return thisZero;
    }

    private static void UpdateTree(FgkStream h, uint n)
    {
        FgkNode incrNode = h.Alphabet[n].Weight == 0 ? IncreaseZeroWeight(h, n) : h.Alphabet[n];

        while (incrNode != h.RootNode)
        {
            MoveRight(h, incrNode);
            Promote(h, incrNode);

            incrNode.Weight += 1;
            incrNode = incrNode.Parent!;
        }

        h.RootNode!.Weight += 1;
    }

    private static void MoveRight(FgkStream h, FgkNode moveFwd)
    {
        FgkNode moveBack = moveFwd.MyBlock!.Leader!;

        if (moveFwd == moveBack || moveFwd.Parent == moveBack || moveFwd.Weight == 0)
            return;

        moveBack.Right!.Left = moveFwd;

        if (moveFwd.Left != null)
            moveFwd.Left.Right = moveBack;

        FgkNode? tmp = moveFwd.Right;
        moveFwd.Right = moveBack.Right;

        if (tmp == moveBack)
        {
            moveBack.Right = moveFwd;
        }
        else
        {
            tmp!.Left = moveBack;
            moveBack.Right = tmp;
        }

        tmp = moveBack.Left;
        moveBack.Left = moveFwd.Left;

        if (tmp == moveFwd)
            moveFwd.Left = moveBack;
        else
        {
            tmp!.Right = moveFwd;
            moveFwd.Left = tmp;
        }

        FgkNode fwdOldParent = moveFwd.Parent!;
        FgkNode backOldParent = moveBack.Parent!;
        bool fwdWasRightChild = fwdOldParent.RightChild == moveFwd;
        bool backWasRightChild = backOldParent.RightChild == moveBack;

        moveFwd.Parent = backOldParent;
        moveBack.Parent = fwdOldParent;

        if (fwdWasRightChild)
            fwdOldParent.RightChild = moveBack;
        else
            fwdOldParent.LeftChild = moveBack;

        if (backWasRightChild)
            backOldParent.RightChild = moveFwd;
        else
            backOldParent.LeftChild = moveFwd;

        moveFwd.MyBlock!.Leader = moveFwd;
    }

    private static void Promote(FgkStream h, FgkNode node)
    {
        FgkNode? myRight = node.Right;
        FgkNode? myLeft = node.Left;
        FgkBlock? curBlock = node.MyBlock;

        if (node.Weight == 0)
            return;

        if (myLeft == node.RightChild && node.LeftChild != null && node.LeftChild.Weight == 0)
        {
            if (node.Weight == myRight!.Weight - 1 && myRight != h.RootNode)
            {
                FreeBlock(curBlock!);

                node.MyBlock = myRight.MyBlock;
                myLeft!.MyBlock = myRight.MyBlock;
            }

            return;
        }

        if (myLeft == h.RemainingZeros)
            return;

        if (myLeft!.MyBlock == curBlock)
            myLeft.MyBlock!.Leader = myLeft;
        else
            FreeBlock(curBlock!);

        if (node.Weight == myRight!.Weight - 1 && myRight != h.RootNode)
            node.MyBlock = myRight.MyBlock;
        else
            node.MyBlock = MakeBlock(node);
    }

    private static bool DecodeBit(FgkStream h, uint b)
    {
        if (h.DecodePtr!.Weight == 0)
        {
            uint bitsreq = h.ZeroFreqRem == 0 ? h.ZeroFreqExp : h.ZeroFreqExp + 1;

            h.CodedBits[h.CodedDepth] = (byte)b;
            h.CodedDepth += 1;

            return h.CodedDepth >= bitsreq;
        }

        h.DecodePtr = b != 0 ? h.DecodePtr.RightChild : h.DecodePtr.LeftChild;

        if (h.DecodePtr!.LeftChild == null)
        {
            if (h.DecodePtr.Weight != 0)
                return true;

            return h.ZeroFreqCount == 1;
        }

        return false;
    }

    private static uint NthZero(FgkStream h, uint n)
    {
        FgkNode ret = h.RemainingZeros!;

        while (n != 0 && ret.RightChild != null)
        {
            ret = ret.RightChild;
            n -= 1;
        }

        return (uint)ret.Index;
    }

    private static uint DecodeData(FgkStream h)
    {
        uint elt = (uint)h.DecodePtr!.Index;

        if (h.DecodePtr.Weight == 0)
        {
            uint i = 0;
            uint n = 0;

            if (h.CodedDepth > 0)
            {
                for (; i < h.CodedDepth - 1; i++)
                {
                    n |= h.CodedBits[i];
                    n <<= 1;
                }
            }

            n |= h.CodedBits[i];
            elt = NthZero(h, n);
        }

        h.CodedDepth = 0;

        UpdateTree(h, elt);

        h.DecodePtr = h.RootNode;

        return elt;
    }
}