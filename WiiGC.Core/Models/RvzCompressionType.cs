namespace WiiGC.Core.Models;

internal enum RvzCompressionType : uint
{
    None = 0,

    Purge = 1,

    Bzip2 = 2,

    Lzma = 3,

    Lzma2 = 4,

    Zstd = 5
}