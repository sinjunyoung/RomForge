using CHD.Core.Interop.Enums;
using System.Runtime.InteropServices;

namespace CHD.Core.Interop;

public static class LibChdr
{
    #region Dll Imports

    private const string DllName = "chdr.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ChdrError chd_open([MarshalAs(UnmanagedType.LPStr)] string filename, int mode, IntPtr parent, out IntPtr chd);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void chd_close(IntPtr chd);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr chd_get_header(IntPtr chd);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ChdrError chd_read(IntPtr chd, uint hunknum, IntPtr buffer);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ChdrError chd_get_metadata(IntPtr chd, uint searchtag, uint searchindex, IntPtr output, uint outputlen, out uint resultlen, out uint resulttag, out byte resultflags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ChdrError chd_codec_config(IntPtr chd, int param, IntPtr config);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr chd_get_codec_name(uint codec);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr chd_error_string(ChdrError err);

    #endregion

    public const uint CHD_CODEC_NONE = 0;

    public const uint CHD_CODEC_ZLIB = 1;

    public const uint CHD_CODEC_LZMA = 2;

    public const uint CHD_CODEC_HUFFMAN = 3;

    public const uint CHD_CODEC_FLAC = 4;

    public const uint CHD_CODEC_CD_ZLIB = 5;

    public const uint CHD_CODEC_CD_LZMA = 6;

    public const uint CHD_CODEC_CD_FLAC = 7;

    public const uint CHD_CODEC_AVHUFF = 8;


    public const uint CDROM_OLD_METADATA = 0x43484344;

    public const uint CDROM_TRACK_METADATA = 0x43485452;

    public const uint CDROM_TRACK_METADATA2 = 0x43485432;

    public const uint GDROM_OLD_METADATA = 0x43484744;

    public const uint GDROM_TRACK_METADATA = 0x43484754;

    public const uint HARD_DISK_METADATA = 0x48444944;

    public const uint HARD_DISK_IDENT_METADATA = 0x4449444E;

    public const uint PCMCIA_CIS_METADATA = 0x43495320;

    public const uint AV_METADATA = 0x41564156;

    public const uint AV_LD_METADATA = 0x41564C44;

    public static uint GetCodecId(string codecName)
    {
        return codecName?.ToLowerInvariant() switch
        {
            "none" => CHD_CODEC_NONE,
            "zlib" => CHD_CODEC_ZLIB,
            "lzma" => CHD_CODEC_LZMA,
            "huff" => CHD_CODEC_HUFFMAN,
            "flac" => CHD_CODEC_FLAC,
            "cdzl" => CHD_CODEC_CD_ZLIB,
            "cdlz" => CHD_CODEC_CD_LZMA,
            "cdfl" => CHD_CODEC_CD_FLAC,
            "avhu" => CHD_CODEC_AVHUFF,
            _ => CHD_CODEC_NONE
        };
    }

    public static string GetCodecName(uint codecId)
    {
        IntPtr ptr = chd_get_codec_name(codecId);

        if (ptr == IntPtr.Zero)
            return "Unknown";

        return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
    }
}