using System.Buffers.Binary;
using System.Text;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiFileSystem
{
    private const int BootSize = 0x440;
    private const int MaxFstSize = 0x4000000;

    public static WiiPartitionInfo Read(WiiPartitionReader reader)
    {
        byte[] boot = new byte[BootSize];

        reader.Read(0, boot);

        string gameId = Encoding.ASCII.GetString(boot, 0, 6);
        string title = ReadString(boot, 0x20, 0x40);
        long dolOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x420)) << 2;
        long fstOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x424)) << 2;
        long fstSize = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x428)) << 2;

        if (fstSize < 12 || fstSize > MaxFstSize || fstOffset + fstSize > reader.Length)
            throw new InvalidDataException("FST 위치 또는 크기가 올바르지 않습니다.");

        byte[] fst = new byte[fstSize];

        reader.Read(fstOffset, fst);

        var files = WiiFstTree.Parse(fst).EnumerateFiles().Select(static file => new WiiFileEntry(file.Path, file.Node.Offset, file.Node.Size)).ToList();

        return new WiiPartitionInfo(gameId, title, dolOffset, fstOffset, fstSize, files);
    }

    private static string ReadString(byte[] buffer, int start, int max)
    {
        int end = Array.IndexOf(buffer, (byte)0, start, max);
        int length = end < 0 ? max : end - start;

        return Encoding.UTF8.GetString(buffer, start, length);
    }
}