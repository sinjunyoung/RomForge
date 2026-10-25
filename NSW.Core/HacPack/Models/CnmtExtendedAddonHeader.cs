using System.Runtime.InteropServices;

namespace NSW.HacPack.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CnmtExtendedAddonHeader
{
    public ulong ApplicationTitleId;

    public uint RequiredApplicationVersion;

    public uint Padding;
}