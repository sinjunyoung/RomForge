using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CnmtHeader
{
    public ulong TitleId;

    public uint TitleVersion;

    public byte Type;

    public byte _0xD;

    public ushort ExtendedHeaderSize;

    public ushort ContentEntryCount;

    public ushort MetaEntryCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xC)]

    public byte[] _0x14;
}