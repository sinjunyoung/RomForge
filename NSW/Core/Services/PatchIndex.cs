using Patch.Core.Enums;
using Patch.Core.Interfaces;
using Patch.Core.Services;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class PatchIndex
{
    public const string ExefsPrefix = "exefs";

    public const string RomfsPrefix = "romfs";

    private const string LooseOnlyDir = "?loose";

    public static PatchFileIndex FromFolder(string patchPath)
    {
        string? exefsDir = PatchFolderResolver.FindSubDir(patchPath, ExefsPrefix);
        string? romfsDir = PatchFolderResolver.FindSubDir(patchPath, RomfsPrefix);
        return Remap(PatchFileIndex.Build(patchPath), ToRelative(patchPath, exefsDir), ToRelative(patchPath, romfsDir));
    }

    public static PatchFileIndex FromArchive(IArchivePatchSource archive)
    {
        string? exefsPrefix = ArchivePatchFolderResolver.FindSubDir(archive.EntryPaths, ExefsPrefix);
        string? romfsPrefix = ArchivePatchFolderResolver.FindSubDir(archive.EntryPaths, RomfsPrefix);
        return Remap(PatchFileIndex.Build(archive, string.Empty), exefsPrefix?.TrimEnd('/'), romfsPrefix?.TrimEnd('/'));
    }

    private static string? ToRelative(string patchPath, string? dir) => dir == null ? null : Path.GetRelativePath(patchPath, dir).Replace('\\', '/');

    private static PatchFileIndex Remap(PatchFileIndex full, string? exefsRel, string? romfsRel)
    {
        var result = new PatchFileIndex();

        foreach (var entry in full.Entries)
        {
            string relDir = entry.RelativeDir.Replace('\\', '/');

            if (TryRemap(relDir, exefsRel, ExefsPrefix, out string mapped) || TryRemap(relDir, romfsRel, RomfsPrefix, out mapped))
                result.Entries.Add(entry with { RelativeDir = mapped });
            else if (entry.Kind == PatchFileKind.BinaryPatch)
                result.Entries.Add(entry with { RelativeDir = LooseOnlyDir });
        }

        return result;
    }

    private static bool TryRemap(string relDir, string? subDirRel, string prefix, out string mapped)
    {
        mapped = string.Empty;

        if (subDirRel == null)
            return false;

        if (relDir.Equals(subDirRel, StringComparison.OrdinalIgnoreCase))
        {
            mapped = prefix;
            return true;
        }

        if (!relDir.StartsWith(subDirRel + "/", StringComparison.OrdinalIgnoreCase))
            return false;

        mapped = $"{prefix}/{relDir[(subDirRel.Length + 1)..]}";

        return true;
    }
}