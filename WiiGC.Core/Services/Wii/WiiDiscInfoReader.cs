using System.Text;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

public static class WiiDiscInfoReader
{
    private const int HeaderSize = 0x60;

    public static WiiDiscInfo Read(string path)
    {
        var container = DiscImageInspector.DetectContainer(path);
        using var source = RvzInputSource.Open(path);

        if (source.Length < HeaderSize)
            throw new InvalidDataException("디스크 헤더를 읽을 수 없습니다.");

        byte[] header = new byte[HeaderSize];

        source.Read(0, header);

        if (!DiscHeader.IsWii(header))
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        string gameId = Encoding.ASCII.GetString(header, 0, 6);
        string title = Encoding.ASCII.GetString(header, 0x20, 0x40).TrimEnd('\0').Trim();

        return new WiiDiscInfo(gameId, title, header[6], header[7], container, new FileInfo(path).Length, source.Length);
    }
}