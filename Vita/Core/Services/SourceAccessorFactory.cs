using Common.Localization;

namespace Vita.Core.Services;

public static class SourceAccessorFactory
{
    public static ISourceAccessor Open(string path)
    {
        if (Directory.Exists(path))
            return new FolderSourceAccessor(path);

        if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            return new ZipSourceAccessor(path);

        throw new NotSupportedException(Loc.S.ErrVitaUnsupportedSourceType(path));
    }
}