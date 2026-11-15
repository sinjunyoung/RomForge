using Common.Localization;

using CD.Core.Interfaces;

namespace CD.Core.Services.Readers;

public static class DiscImageReaderFactory
{
    private static readonly IDiscImageReader[] Readers = [ new MdfMdsReader(), new CcdImgSubReader(), new CueSheetReader(), ];

    public static IDiscImageReader Resolve(string filePath)
    {
        var reader = Readers.FirstOrDefault(r => r.CanRead(filePath));
        return reader ?? throw new NotSupportedException(Loc.S.ErrUnsupportedSourceFormat(filePath));
    }
}