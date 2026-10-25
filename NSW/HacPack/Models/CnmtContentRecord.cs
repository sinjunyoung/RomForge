using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CnmtContentRecord
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x20)]
    public byte[] Hash;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
    public byte[] NcaId;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x06)]
    public byte[] Size;

    public byte Type;

    public byte IdOffset;
}