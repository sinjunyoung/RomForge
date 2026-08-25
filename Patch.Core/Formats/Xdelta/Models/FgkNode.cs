namespace Patch.Core.Formats.Xdelta.Models;

internal sealed class FgkNode
{
    public int Index;
    public uint Weight;
    public FgkNode? Parent;
    public FgkNode? LeftChild;
    public FgkNode? RightChild;
    public FgkNode? Left;
    public FgkNode? Right;
    public FgkBlock? MyBlock;
}