using CHD.Core.Interop.Enums;
using CHD.Core.Interop.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace CHD.Core.Interop;

public class LibChdrWrapper : IDisposable
{
    private IntPtr _chdHandle = IntPtr.Zero;
    private IntPtr _hunkBuffer = IntPtr.Zero;
    private int _hunkBufferSize;
    private bool _disposed = false;

    public ChdrHeader? Header { get; private set; }

    public bool IsOpen => _chdHandle != IntPtr.Zero;

    public ChdrError Open(string filename, ChdrOpenFlags flags = ChdrOpenFlags.CHDOPEN_READ)
    {
        Close();

        var result = LibChdr.chd_open(filename, (int)flags, IntPtr.Zero, out _chdHandle);

        if (result == ChdrError.CHDERR_NONE)
        {
            IntPtr headerPtr = LibChdr.chd_get_header(_chdHandle);

            if (headerPtr != IntPtr.Zero)
                Header = Marshal.PtrToStructure<ChdrHeader>(headerPtr);
        }

        return result;
    }

    public static string GetCompressionName(uint compression)
    {
        if (compression == 0) 
            return "None";

        var bytes = BitConverter.GetBytes(compression).Reverse().ToArray();

        return new string([.. bytes.Select(b => (char)b)]).TrimEnd('\0');
    }

    public void Close()
    {
        if (_chdHandle != IntPtr.Zero)
        {
            LibChdr.chd_close(_chdHandle);

            _chdHandle = IntPtr.Zero;
            Header = null;
        }

        if (_hunkBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_hunkBuffer);

            _hunkBuffer = IntPtr.Zero;
            _hunkBufferSize = 0;
        }
    }

    public string[] GetCompressionMethods()
    {
        if (!Header.HasValue)
            return [];

        var methods = new List<string>();
        var header = Header.Value;

        if (header.compression0 != 0)
            methods.Add(LibChdr.GetCodecName(header.compression0));

        if (header.compression1 != 0)
            methods.Add(LibChdr.GetCodecName(header.compression1));

        if (header.compression2 != 0)
            methods.Add(LibChdr.GetCodecName(header.compression2));

        if (header.compression3 != 0)
            methods.Add(LibChdr.GetCodecName(header.compression3));

        return [.. methods];
    }

    public byte[] ReadHunk(uint hunkIndex)
    {
        if (!Header.HasValue)
            throw new InvalidOperationException("CHD not opened");

        byte[] buffer = new byte[Header.Value.hunkbytes];
        IntPtr bufferPtr = Marshal.AllocHGlobal((int)Header.Value.hunkbytes);

        try
        {
            var result = LibChdr.chd_read(_chdHandle, hunkIndex, bufferPtr);

            if (result != ChdrError.CHDERR_NONE)
                throw new Exception($"Read error: {GetErrorString(result)}");

            Marshal.Copy(bufferPtr, buffer, 0, buffer.Length);

            return buffer;
        }
        finally
        {
            Marshal.FreeHGlobal(bufferPtr);
        }
    }

    public void ReadHunkInto(uint hunkIndex, byte[] destination)
    {
        if (!Header.HasValue)
            throw new InvalidOperationException("CHD not opened");

        int size = (int)Header.Value.hunkbytes;

        if (destination.Length < size)
            throw new ArgumentException("Destination buffer is smaller than the hunk size", nameof(destination));

        if (_hunkBuffer == IntPtr.Zero || _hunkBufferSize != size)
        {
            if (_hunkBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(_hunkBuffer);

            _hunkBuffer = Marshal.AllocHGlobal(size);
            _hunkBufferSize = size;
        }

        var result = LibChdr.chd_read(_chdHandle, hunkIndex, _hunkBuffer);

        if (result != ChdrError.CHDERR_NONE)
            throw new Exception($"Read error: {GetErrorString(result)}");

        Marshal.Copy(_hunkBuffer, destination, 0, size);
    }

    public string GetMetadata(uint tag, uint index = 0)
    {
        if (!IsOpen)
            throw new InvalidOperationException("CHD not opened");

        const int bufferSize = 65536;
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            var result = LibChdr.chd_get_metadata(_chdHandle, tag, index, buffer, bufferSize, out uint resultLen, out uint resultTag, out byte flags);

            if (result == ChdrError.CHDERR_NONE && resultLen > 0)
            {
                byte[] data = new byte[resultLen];

                Marshal.Copy(buffer, data, 0, (int)resultLen);

                return Encoding.ASCII.GetString(data).TrimEnd('\0');
            }
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string GetErrorString(ChdrError error)
    {
        IntPtr ptr = LibChdr.chd_error_string(error);

        if (ptr == IntPtr.Zero)
            return error.ToString();

        return Marshal.PtrToStringAnsi(ptr) ?? error.ToString();
    }

    public static string ByteArrayToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(bytes.Length * 2);

        foreach (byte b in bytes)
            sb.AppendFormat("{0:x2}", b);

        return sb.ToString();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    ~LibChdrWrapper() => Dispose();
}