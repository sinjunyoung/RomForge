using System.Buffers.Binary;
using WiiGC.Core.Models;
using WiiGC.Core.Services.GameCube;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

public static class DiscImageInspector
{
    public static DiscPlatform Detect(string path)
    {
        using var source = RvzInputSource.Open(path);

        if (source.Length < 0x20)
            return DiscPlatform.Unknown;

        Span<byte> header = stackalloc byte[0x20];

        source.Read(0, header);

        if (BinaryPrimitives.ReadUInt32BigEndian(header[0x18..]) == 0x5D1C9EA3)
            return DiscPlatform.Wii;

        if (BinaryPrimitives.ReadUInt32BigEndian(header[0x1C..]) == 0xC2339F3D)
            return DiscPlatform.GameCube;

        return DiscPlatform.Unknown;
    }

    public static bool IsWiiRvz(string path)
    {
        try
        {
            using var reader = new RvzDiscReader(path);

            return reader.DiscType == 2;
        }
        catch
        {
            return false;
        }
    }

    public static DiscContainerFormat DetectContainer(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (RandomAccess.GetLength(handle) < 4)
            return DiscContainerFormat.Unknown;

        if (GczSource.IsGcz(handle))
            return DiscContainerFormat.Gcz;

        if (WbfsSource.IsWbfs(handle))
            return DiscContainerFormat.Wbfs;

        Span<byte> header = stackalloc byte[4];

        RandomAccess.Read(handle, header, 0);

        if (RvzMagic.IsRvz(header))
            return DiscContainerFormat.Rvz;

        if (RvzMagic.IsWia(header))
            return DiscContainerFormat.Wia;

        return DiscContainerFormat.PlainDisc;
    }
}