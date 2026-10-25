using System.Runtime.InteropServices;

namespace PSP.Core.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CsoHeader
{
    public static readonly byte[] MagicCSO = "CISO"u8.ToArray();

    public static readonly byte[] MagicZSO = "ZISO"u8.ToArray();

    public uint HeaderSize;

    public ulong UncompressedSize;

    public uint BlockSize;

    public byte Version;

    public byte IndexShift;

    public ushort Unused;
}