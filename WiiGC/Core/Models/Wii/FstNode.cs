namespace WiiGC.Core.Models.Wii;

internal sealed class FstNode(byte[] nameBytes, bool isDirectory)
{
    public byte[] NameBytes { get; } = nameBytes;

    public bool IsDirectory { get; } = isDirectory;

    public List<FstNode> Children { get; } = [];

    public long Offset { get; set; }

    public long Size { get; set; }

    public string Name => FstName.Decode(NameBytes);
}