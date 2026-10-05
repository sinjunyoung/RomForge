namespace WiiGC.Core.Models;

internal static class WiiFolderLayout
{
    public const string Files = "files";

    public const string Sys = "sys";

    public const string Meta = "meta";

    public const string Boot = "boot.bin";

    public const string Bi2 = "bi2.bin";

    public const string Apploader = "apploader.img";

    public const string Dol = "main.dol";

    public const string Disc = "disc.bin";

    public const string Partition = "partition.bin";

    public const string Tmd = "tmd.bin";

    public const string Cert = "cert.bin";

    public const int DiscHeadSize = 0x50000;

    public const int BootSize = 0x440;

    public const int Bi2Size = 0x2000;

    public const int ApploaderOffset = 0x2440;

    public const int PartitionHeaderSize = 0x2C0;
}