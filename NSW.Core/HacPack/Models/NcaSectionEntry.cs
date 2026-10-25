using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NcaSectionEntry
{
    public uint MediaStartOffset;

    public uint MediaEndOffset;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x8)]
    public byte[] _0x8;
}