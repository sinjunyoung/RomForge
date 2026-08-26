namespace Patch.Core.Formats.Xdelta.Services;

public interface IXd3RandomAccessSource
{
    void ReadAt(long offset, byte[] dest, int destOffset, int count);
}