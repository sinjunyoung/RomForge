namespace DolphinTool.Core.Models;

internal static class WiiLayout
{
    public const int BlocksPerGroup = 64;

    public const int BlockHeaderSize = 0x400;

    public const int BlockDataSize = 0x7C00;

    public const int BlockTotalSize = 0x8000;

    public const int GroupHeaderSize = BlocksPerGroup * BlockHeaderSize;

    public const int GroupDataSize = BlocksPerGroup * BlockDataSize;

    public const int GroupTotalSize = BlocksPerGroup * BlockTotalSize;

    public const int HashSize = 20;

    public const int H0Count = 31;

    public const int H0Bytes = H0Count * HashSize;

    public const int H1Offset = 0x280;

    public const int H1Bytes = 8 * HashSize;

    public const int H2Offset = 0x340;

    public const int H2Bytes = 8 * HashSize;

    public const int LfgBlockSize = 0x8000;
}