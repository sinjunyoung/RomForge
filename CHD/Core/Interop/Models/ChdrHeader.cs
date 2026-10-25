using System.Runtime.InteropServices;

namespace CHD.Core.Interop.Models;

[StructLayout(LayoutKind.Sequential)]
public struct ChdrHeader
{
    public uint length;

    public uint version;

    public uint flags;

    public uint compression0;

    public uint compression1;

    public uint compression2;

    public uint compression3;

    public uint hunkbytes;

    public uint totalhunks;

    public ulong logicalbytes;

    public ulong metaoffset;

    public ulong mapoffset;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] md5;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] parentmd5;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] sha1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] rawsha1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] parentsha1;

    public uint unitbytes;

    public ulong unitcount;

    public uint hunkcount;
}