namespace WiiU.Core.Models;

public sealed class WupFileEntry(string relativePath, Func<Stream> openRead, long length)
{
    public string RelativePath { get; } = relativePath;

    public Func<Stream> OpenRead { get; } = openRead;

    public long Length { get; } = length;
}