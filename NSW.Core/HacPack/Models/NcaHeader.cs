using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NcaHeader
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
    public byte[] FixedKeySig;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
    public byte[] NpdmKeySig;

    public uint Magic;

    public byte Distribution;

    public byte ContentType;

    public byte CryptoType;

    public byte KaekInd;

    public ulong NcaSize;

    public ulong TitleId;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x4)]
    public byte[] _0x218;

    public uint SdkVersion;

    public byte CryptoType2;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xF)]
    public byte[] _0x221;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
    public byte[] RightsId;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public NcaSectionEntry[] SectionEntries;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4 * 0x20)]
    public byte[] SectionHashes;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4 * 0x10)]
    public byte[] EncryptedKeys;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xC0)]
    public byte[] _0x340;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public NcaFsHeader[] FsHeaders;
}