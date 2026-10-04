using System.Text;

namespace DolphinTool.Core.Models;

internal sealed class WiiFstNode(byte[] nameBytes, bool isDirectory)
{
    public byte[] NameBytes { get; } = nameBytes;

    public bool IsDirectory { get; } = isDirectory;

    public List<WiiFstNode> Children { get; } = [];

    public long Offset { get; set; }

    public long Size { get; set; }

    public string Name => Encoding.UTF8.GetString(NameBytes);
}