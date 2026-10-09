using WiiGC.Core.Models;
using WiiGC.Core.Services.GameCube;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

public static class DiscImageInspector
{
    public static DiscPlatform Detect(string path)
    {
        using var source = RvzInputSource.Open(path);

        if (source.Length < DiscHeader.Size)
            return DiscPlatform.Unknown;

        Span<byte> header = stackalloc byte[DiscHeader.Size];

        source.Read(0, header);

        return DiscHeader.Detect(header);
    }

    public static bool IsWiiRvz(string path)
    {
        try
        {
            using var reader = new RvzDiscReader(path);

            return DiscHeader.FromRvzType(reader.DiscType) == DiscPlatform.Wii;
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