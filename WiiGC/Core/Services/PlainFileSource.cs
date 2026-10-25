using Microsoft.Win32.SafeHandles;
using WiiGC.Core.Interfaces;

namespace WiiGC.Core.Services;

internal sealed class PlainFileSource(SafeFileHandle handle) : IRvzInputSource
{
    public long Length { get; } = RandomAccess.GetLength(handle);

    public void Read(long offset, Span<byte> destination) => RvzIo.ReadExactly(handle, destination, offset);

    public void Dispose() => handle.Dispose();
}