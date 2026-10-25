using System.Runtime.InteropServices;
namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NpdmHeader
{
    public uint Magic;

    public uint _0x4;

    public uint _0x8;

    public byte MmuFlags;

    public byte _0xD;

    public byte MainThreadPrio;

    public byte DefaultCpuId;

    public ulong _0x10;

    public uint ProcessCategory;

    public uint MainStackSize;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x50)]
    public byte[] TitleName;

    public uint Aci0Offset;

    public uint Aci0Size;

    public uint AcidOffset;

    public uint AcidSize;
}