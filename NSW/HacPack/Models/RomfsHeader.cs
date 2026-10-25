using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RomfsHeader
{
    public ulong HeaderSize;

    public ulong DirHashTableOfs;

    public ulong DirHashTableSize;

    public ulong DirTableOfs;

    public ulong DirTableSize;

    public ulong FileHashTableOfs;

    public ulong FileHashTableSize;

    public ulong FileTableOfs;

    public ulong FileTableSize;

    public ulong FilePartitionOfs;
}