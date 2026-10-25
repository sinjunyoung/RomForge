using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NpdmAci0
{
    public uint Magic;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xC)]
    public byte[] _0x4;

    public ulong TitleId;

    public ulong _0x18;

    public uint FahOffset;

    public uint FahSize;

    public uint SacOffset;

    public uint SacSize;

    public uint KacOffset;

    public uint KacSize;

    public ulong Padding;
}