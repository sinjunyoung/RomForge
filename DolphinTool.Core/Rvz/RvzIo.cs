using Microsoft.Win32.SafeHandles;

namespace DolphinTool.Core.Rvz;

internal static class RvzIo
{
    public static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long offset)
    {
        int total = 0;

        while (total < buffer.Length)
        {
            int read = RandomAccess.Read(handle, buffer[total..], offset + total);

            if (read <= 0)
                throw new EndOfStreamException("RVZ 파일이 예상보다 일찍 끝났습니다.");

            total += read;
        }
    }
}