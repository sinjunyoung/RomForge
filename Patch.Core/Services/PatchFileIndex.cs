namespace Patch.Core.Services;

public enum PatchFileKind { Overwrite, BinaryPatch }

public record PatchFileEntry(PatchFileRef File, string RelativeDir, string BaseName, PatchFileKind Kind);

public class PatchFileIndex
{
    public static readonly string[] PatchExtensions = [".xdelta", ".xdelta3", ".ips", ".ups", ".bps", ".ppf", ".aps"];

    public List<PatchFileEntry> Entries { get; } = [];

    public bool HasAnyFile => Entries.Count > 0;

    public static PatchFileIndex Build(string patchDir)
    {
        var index = new PatchFileIndex();

        if (!Directory.Exists(patchDir))
            return index;

        foreach (string file in Directory.EnumerateFiles(patchDir, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            bool isPatch = PatchExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            string rel = Path.GetRelativePath(patchDir, file);
            string relDir = Path.GetDirectoryName(rel) ?? string.Empty;
            string baseName = isPatch ? Path.GetFileNameWithoutExtension(rel) : Path.GetFileName(rel);

            index.Entries.Add(new PatchFileEntry(PatchFileRef.FromDisk(file), relDir, baseName, isPatch ? PatchFileKind.BinaryPatch : PatchFileKind.Overwrite));
        }

        return index;
    }

    public static PatchFileIndex Build(IArchivePatchSource archive, string prefix)
    {
        var index = new PatchFileIndex();
        string normalizedPrefix = prefix.Length == 0 ? string.Empty : prefix.TrimEnd('/') + "/";

        foreach (string key in archive.EntryPaths)
        {
            if (!key.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string rel = key[normalizedPrefix.Length..];

            if (rel.Length == 0)
                continue;

            string ext = Path.GetExtension(rel);
            bool isPatch = PatchExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            int lastSlash = rel.LastIndexOf('/');
            string relDir = lastSlash < 0 ? string.Empty : rel[..lastSlash];
            string fileName = lastSlash < 0 ? rel : rel[(lastSlash + 1)..];
            string baseName = isPatch ? Path.GetFileNameWithoutExtension(fileName) : fileName;
            var entry = archive.FindEntry(key) ?? throw new InvalidOperationException($"아카이브 엔트리를 다시 찾을 수 없습니다: {key}");
            var fileRef = PatchFileRef.FromArchiveEntry(entry);

            index.Entries.Add(new PatchFileEntry(fileRef, relDir, baseName, isPatch ? PatchFileKind.BinaryPatch : PatchFileKind.Overwrite));
        }

        return index;
    }

    public static PatchFileIndex BuildTopLevelOnly(string dir)
    {
        var index = new PatchFileIndex();

        if (!Directory.Exists(dir))
            return index;

        foreach (string file in Directory.EnumerateFiles(dir))
        {
            string ext = Path.GetExtension(file);
            bool isPatch = PatchExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            string fileName = Path.GetFileName(file);
            string baseName = isPatch ? Path.GetFileNameWithoutExtension(fileName) : fileName;

            index.Entries.Add(new PatchFileEntry(PatchFileRef.FromDisk(file), string.Empty, baseName, isPatch ? PatchFileKind.BinaryPatch : PatchFileKind.Overwrite));
        }

        return index;
    }

    public static PatchFileIndex BuildTopLevelOnly(IArchivePatchSource archive, string prefix)
    {
        var index = new PatchFileIndex();
        string normalizedPrefix = prefix.Length == 0 ? string.Empty : prefix.TrimEnd('/') + "/";

        foreach (string key in archive.EntryPaths)
        {
            if (!key.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string rel = key[normalizedPrefix.Length..];

            if (rel.Length == 0 || rel.Contains('/'))
                continue;

            string ext = Path.GetExtension(rel);
            bool isPatch = PatchExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            string baseName = isPatch ? Path.GetFileNameWithoutExtension(rel) : rel;
            var entry = archive.FindEntry(key) ?? throw new InvalidOperationException($"아카이브 엔트리를 다시 찾을 수 없습니다: {key}");
            var fileRef = PatchFileRef.FromArchiveEntry(entry);

            index.Entries.Add(new PatchFileEntry(fileRef, string.Empty, baseName, isPatch ? PatchFileKind.BinaryPatch : PatchFileKind.Overwrite));
        }

        return index;
    }
}