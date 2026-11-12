using Common.Localization;

using Microsoft.Win32.SafeHandles;

namespace WiiGC.Core.Services;

internal static class RvzIo
{
    public static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long offset)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int read = RandomAccess.Read(handle, buffer[total..], offset + total);

            if (read <= 0)
                throw new EndOfStreamException(Loc.S.ErrRvzFileEndedEarly);

            total += read;
        }
    }
}