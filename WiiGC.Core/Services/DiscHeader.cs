using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal static class DiscHeader
{
    public const int Size = 0x20;

    public const uint RvzTypeGameCube = 1;

    public const uint RvzTypeWii = 2;

    private const int WiiMagicOffset = 0x18;

    private const int GameCubeMagicOffset = 0x1C;

    private const uint WiiMagic = 0x5D1C9EA3;

    private const uint GameCubeMagic = 0xC2339F3D;

    public static DiscPlatform Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length < Size)
            return DiscPlatform.Unknown;

        if (BinaryPrimitives.ReadUInt32BigEndian(header[WiiMagicOffset..]) == WiiMagic)
            return DiscPlatform.Wii;

        if (BinaryPrimitives.ReadUInt32BigEndian(header[GameCubeMagicOffset..]) == GameCubeMagic)
            return DiscPlatform.GameCube;

        return DiscPlatform.Unknown;
    }

    public static bool IsWii(ReadOnlySpan<byte> header) => Detect(header) == DiscPlatform.Wii;

    public static DiscPlatform FromRvzType(uint rvzType) => rvzType switch
    {
        RvzTypeGameCube => DiscPlatform.GameCube,
        RvzTypeWii => DiscPlatform.Wii,
        _ => DiscPlatform.Unknown
    };

}