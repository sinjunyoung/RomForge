namespace Vita.Core.Services;

public static class VitaSourceAccessorFactory
{
    public static IVitaSourceAccessor Open(string path)
    {
        if (Directory.Exists(path))
            return new FolderSourceAccessor(path);

        if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            return new ZipSourceAccessor(path);

        throw new NotSupportedException($"지원하지 않는 소스 형식입니다: {path} (폴더 / zip만 지원)");
    }
}