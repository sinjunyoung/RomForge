using Patch.Core.Formats.Xdelta.Models;

namespace Patch.Core.Formats.Xdelta.Services;

internal sealed class FgkStream
{
    public uint AlphabetSize;
    public uint ZeroFreqCount;
    public uint ZeroFreqExp;
    public uint ZeroFreqRem;
    public uint CodedDepth;

    public byte[] CodedBits = [];
    public FgkNode[] Alphabet = [];
    public int FreeNodeIndex;

    public FgkNode? DecodePtr;
    public FgkNode? RemainingZeros;
    public FgkNode? RootNode;
}