using Microsoft.Win32.SafeHandles;

namespace WiiGC.Core.Services;

internal sealed class FileIsoSink(SafeFileHandle handle) : IIsoSink
{
    public void SetLength(long length) => RandomAccess.SetLength(handle, length);

    public void Write(long offset, ReadOnlySpan<byte> data) => RandomAccess.Write(handle, data, offset);
}