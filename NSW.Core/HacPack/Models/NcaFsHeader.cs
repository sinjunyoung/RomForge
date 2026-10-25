using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NcaFsHeader
{
    public ushort Version;

    public byte FsType;

    public byte HashType;

    public byte CryptType;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3)]
    public byte[] _0x5;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x138)]
    public byte[] SuperblockRaw;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x8)]
    public byte[] SectionCtr;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xB8)]
    public byte[] _0x148;
}