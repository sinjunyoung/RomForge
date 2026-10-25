using NSW.HacPack.Models;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NSW.HacPack.Services;

public static class NcaStructHelper
{
    public static void WriteStruct<T>(Stream s, T val) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buf = new byte[size];
        nint ptr = Marshal.AllocHGlobal(size);

        try 
        {
            Marshal.StructureToPtr(val, ptr, false); 
            Marshal.Copy(ptr, buf, 0, size);
        }
        finally 
        {
            Marshal.FreeHGlobal(ptr); 
        }

        s.Write(buf);
    }

    public static byte[] StructToBytes<T>(ref T val) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buf = new byte[size];
        nint ptr = Marshal.AllocHGlobal(size);

        try 
        { 
            Marshal.StructureToPtr(val, ptr, false); 
            Marshal.Copy(ptr, buf, 0, size);
        }
        finally 
        { 
            Marshal.FreeHGlobal(ptr);
        }

        return buf;
    }

    public static void BytesToStruct<T>(byte[] buf, ref T val) where T : struct
    {
        nint ptr = Marshal.AllocHGlobal(buf.Length);

        try 
        { 
            Marshal.Copy(buf, 0, ptr, buf.Length);
            val = Marshal.PtrToStructure<T>(ptr); 
        }
        finally 
        { 
            Marshal.FreeHGlobal(ptr); 
        }
    }

    public static byte[] GetHeaderMagicBytes(ref NcaHeader hdr)
    {
        byte[] raw = StructToBytes(ref hdr);
        byte[] result = new byte[0x200];

        Array.Copy(raw, 0x200, result, 0, 0x200);

        return result;
    }

    public static byte[] CalculateSectionHash(ref NcaHeader hdr, int idx)
    {
        byte[] fsHdrBytes = StructToBytes(ref hdr.FsHeaders[idx]);
        return SHA256.HashData(fsHdrBytes.AsSpan(0, 0x200));
    }

    public static byte[] CalculateNcaHash(FileStream ncaFile)
    {
        ncaFile.Seek(0, SeekOrigin.Begin);
        return SHA256.HashData(ncaFile);
    }
}