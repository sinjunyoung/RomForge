namespace Patch.Core.Models;

public sealed class PatchedContent(long length, Func<Stream> open)
{
    public long Length { get; } = length;

    public Func<Stream> Open { get; } = open;
}