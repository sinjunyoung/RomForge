namespace Patch.Core.Services;

public static class ArchivePatchSourceFactory
{
    private static readonly string[] SupportedExtensions = [".zip", ".7z"];

    public const string ScopeSeparator = "::";

    public static bool IsArchivePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string archivePath = SplitScope(path).ArchivePath;

        return File.Exists(archivePath) && SupportedExtensions.Contains(Path.GetExtension(archivePath), StringComparer.OrdinalIgnoreCase);
    }

    public static (string ArchivePath, string Scope) SplitScope(string path)
    {
        int idx = path.IndexOf(ScopeSeparator, StringComparison.Ordinal);

        return idx < 0 ? (path, string.Empty) : (path[..idx], path[(idx + ScopeSeparator.Length)..]);
    }

    public static string CombineScope(string archivePath, string scope) => string.IsNullOrEmpty(scope) ? archivePath : $"{archivePath}{ScopeSeparator}{scope}";

    public static IArchivePatchSource Open(string path, string? password = null) => new NestedArchivePatchSource(OpenRaw(path, password));

    internal static IArchivePatchSource OpenRaw(string path, string? password = null)
    {
        var (archivePath, scope) = SplitScope(path);
        string ext = Path.GetExtension(archivePath);

        IArchivePatchSource baseArchive = string.Equals(ext, ".7z", StringComparison.OrdinalIgnoreCase)
            ? new SevenZipArchivePatchSource(archivePath, password)
            : string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase)
                ? new ZipArchivePatchSource(archivePath, password)
                : throw new NotSupportedException($"지원하지 않는 압축 형식입니다: {ext}");

        return scope.Length == 0 ? baseArchive : new ScopedArchivePatchSource(baseArchive, scope);
    }
}