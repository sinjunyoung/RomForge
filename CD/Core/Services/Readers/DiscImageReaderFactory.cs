using CD.Core.Interfaces;

namespace CD.Core.Services.Readers;

public static class DiscImageReaderFactory
{
    private static readonly IDiscImageReader[] Readers = [ new MdfMdsReader(), new CcdImgSubReader(), new CueSheetReader(), ];

    public static IDiscImageReader Resolve(string filePath)
    {
        var reader = Readers.FirstOrDefault(r => r.CanRead(filePath));
        return reader ?? throw new NotSupportedException($"지원하지 않는 소스 포맷입니다: {filePath}");
    }
}