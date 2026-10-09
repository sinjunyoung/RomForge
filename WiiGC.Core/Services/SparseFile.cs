using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace WiiGC.Core.Services;

internal static class SparseFile
{
    private const uint FsctlSetSparse = 0x000900C4;

    public static bool TryMark(SafeFileHandle handle)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return DeviceIoControl(handle, FsctlSetSparse, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
    }

    public static SafeFileHandle Create(string path, long length)
    {
        var handle = File.OpenHandle(path, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);

        try
        {
            TryMark(handle);
            RandomAccess.SetLength(handle, length);

            return handle;
        }
        catch
        {
            handle.Dispose();

            throw;
        }
    }

    public static void CopyNonZero(IRvzInputSource input, IIsoSink sink, byte[] buffer, long offset, long count, ProgressReporter? reporter, CancellationToken ct)
    {
        long end = offset + count;

        while (offset < end)
        {
            ct.ThrowIfCancellationRequested();

            int size = (int)Math.Min(buffer.Length, end - offset);
            var span = buffer.AsSpan(0, size);

            input.Read(offset, span);

            if (span.ContainsAnyExcept((byte)0))
                sink.Write(offset, span);

            offset += size;

            reporter?.Add(size);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, IntPtr inBuffer, int inBufferSize, IntPtr outBuffer, int outBufferSize, out int bytesReturned, IntPtr overlapped);
}