
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PkgProbe
{
    public static PkgProbeResult Probe(string pkgPath)
    {
        using var accessor = new PkgSourceAccessor(pkgPath);

        if (!WorkBinReader.TryGetTitleIdFromContentId(accessor.Header.ContentId, out string titleId))
            throw new InvalidDataException($"content_id에서 title id를 추출할 수 없습니다: {accessor.Header.ContentId}");

        return accessor.Header.ContentType switch
        {
            (uint)ContentType.App => new PkgProbeResult
            {
                TitleId = titleId,
                Category = PkgCategoryResolver.ResolveAppOrPatch(accessor),
                ContentId = accessor.Header.ContentId
            },
            (uint)ContentType.Dlc => new PkgProbeResult
            {
                TitleId = titleId,
                Category = ContentCategory.Addcont,
                ContentIdSuffix = accessor.Header.ContentId.Length > 20 ? accessor.Header.ContentId[20..] : accessor.Header.ContentId,
                ContentId = accessor.Header.ContentId
            },
            _ => throw new NotSupportedException($"지원하지 않는 PKG content type: 0x{accessor.Header.ContentType:x}")
        };
    }
} 