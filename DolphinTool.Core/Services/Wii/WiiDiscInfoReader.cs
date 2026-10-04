using DolphinTool.Core.Models;
using System.Buffers.Binary;
using System.Text;

namespace DolphinTool.Core.Services.Wii;

public static class WiiDiscInfoReader
{
    private const int HeaderSize = 0x60;
    private const uint WiiMagic = 0x5D1C9EA3;

    public static WiiDiscInfo Read(string path)
    {
        var container = DiscImageInspector.DetectContainer(path);
        using var source = RvzInputSource.Open(path);

        if (source.Length < HeaderSize)
            throw new InvalidDataException("디스크 헤더를 읽을 수 없습니다.");

        byte[] header = new byte[HeaderSize];

        source.Read(0, header);

        if (BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0x18)) != WiiMagic)
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        string gameId = Encoding.ASCII.GetString(header, 0, 6);
        string title = Encoding.ASCII.GetString(header, 0x20, 0x40).TrimEnd('\0').Trim();

        return new WiiDiscInfo(gameId, title, header[6], header[7], container, new FileInfo(path).Length, source.Length);
    }
}