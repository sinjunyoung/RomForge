using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NpdmAcid
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
    public byte[] Signature;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
    public byte[] Modulus;

    public uint Magic;

    public uint Size;

    public uint _0x208;

    public uint Flags;

    public ulong TitleIdRangeMin;

    public ulong TitleIdRangeMax;

    public uint FacOffset;

    public uint FacSize;

    public uint SacOffset;

    public uint SacSize;

    public uint KacOffset;

    public uint KacSize;

    public ulong Padding;
}