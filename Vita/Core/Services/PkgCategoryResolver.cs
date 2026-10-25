using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PkgCategoryResolver
{
    public static ContentCategory ResolveAppOrPatch(ISourceAccessor source)
    {
        try
        {
            byte[] sfoBytes = source.ReadAllBytes("sce_sys/param.sfo");
            var sfo = SfoParser.Parse(sfoBytes);
            string? category = SfoParser.GetString(sfo, "CATEGORY");

            return string.Equals(category, "gp", StringComparison.OrdinalIgnoreCase) ? ContentCategory.Patch : ContentCategory.App;
        }
        catch
        {
            return ContentCategory.App;
        }
    }
}