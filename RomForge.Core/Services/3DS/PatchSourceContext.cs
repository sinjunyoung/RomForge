using _3DS.Core.Services;
using Common;
using Patch.Core.Services;
using System.IO;

namespace RomForge.Core.Services._3DS;

public sealed class PatchSourceContext : IDisposable
{
    private readonly string? _diskPath;

    private readonly IArchivePatchSource? _archive;

    public bool HasSource { get; }

    private PatchSourceContext(string? diskPath, IArchivePatchSource? archive, bool hasSource)
    {
        _diskPath = diskPath;
        _archive = archive;
        HasSource = hasSource;
    }

    public static PatchSourceContext Open(string? rawPath, Action<string, LogLevel> log)
    {
        if (string.IsNullOrEmpty(rawPath))
            return new PatchSourceContext(null, null, false);

        if (ArchivePatchSourceFactory.IsArchivePath(rawPath))
        {
            try
            {
                return new PatchSourceContext(null, ArchivePatchSourceFactory.Open(rawPath), true);
            }
            catch (Exception ex)
            {
                log($"⚠️ 한글패치 압축파일을 열 수 없습니다: {ex.Message}", LogLevel.Error);

                return new PatchSourceContext(null, null, false);
            }
        }

        if (!Directory.Exists(rawPath))
        {
            log($"⚠️ 한글패치 경로를 찾을 수 없습니다: {rawPath}", LogLevel.Error);

            return new PatchSourceContext(null, null, false);
        }

        return new PatchSourceContext(rawPath, null, true);
    }

    public PatchFileIndex? FindSubIndex(string folderName)
    {
        if (!HasSource)
            return null;

        if (_archive != null)
        {
            string? prefix = ArchivePatchFolderResolver.FindSubDir(_archive.EntryPaths, folderName);

            return prefix == null ? null : PatchFileIndex.Build(_archive, prefix);
        }

        string? dir = PatchFolderResolver.FindSubDir(_diskPath!, folderName);

        return dir == null ? null : PatchFileIndex.Build(dir);
    }

    public PatchFileIndex? RootIndex()
    {
        if (!HasSource)
            return null;

        var combined = new PatchFileIndex();

        if (_archive != null)
        {
            combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(_archive, string.Empty).Entries);

            string? exefsPrefix = ArchivePatchFolderResolver.FindSubDir(_archive.EntryPaths, "exefs");

            if (exefsPrefix != null)
                combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(_archive, ParentPrefix(exefsPrefix)).Entries);

            string? romfsPrefix = ArchivePatchFolderResolver.FindSubDir(_archive.EntryPaths, "romfs");

            if (romfsPrefix != null)
                combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(_archive, ParentPrefix(romfsPrefix)).Entries);

            foreach (string codeFileName in new[] { "code.bin", "code.ips" })
            {
                string? codeParent = FindFileParentPrefix(_archive.EntryPaths, codeFileName);

                if (codeParent != null)
                    combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(_archive, codeParent).Entries);
            }
        }
        else
        {
            combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(_diskPath!).Entries);

            string? exefsDir = PatchFolderResolver.FindSubDir(_diskPath!, "exefs");

            if (exefsDir != null)
                combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(Path.GetDirectoryName(exefsDir)!).Entries);

            string? romfsDir = PatchFolderResolver.FindSubDir(_diskPath!, "romfs");

            if (romfsDir != null)
                combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(Path.GetDirectoryName(romfsDir)!).Entries);

            foreach (string codeFileName in new[] { "code.bin", "code.ips" })
            {
                string? codeParent = FindFileParentDir(_diskPath!, codeFileName);

                if (codeParent != null)
                    combined.Entries.AddRange(PatchFileIndex.BuildTopLevelOnly(codeParent).Entries);
            }
        }

        return combined.HasAnyFile ? combined : null;
    }

    private static string ParentPrefix(string subDirPrefix)
    {
        string trimmed = subDirPrefix.TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');

        return lastSlash < 0 ? string.Empty : trimmed[..(lastSlash + 1)];
    }

    private static string? FindFileParentPrefix(IReadOnlyList<string> entryPaths, string fileName)
    {
        string? best = null;
        int bestDepth = int.MaxValue;

        foreach (string path in entryPaths)
        {
            int lastSlash = path.LastIndexOf('/');
            string name = lastSlash < 0 ? path : path[(lastSlash + 1)..];

            if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
                continue;

            string parent = lastSlash < 0 ? string.Empty : path[..(lastSlash + 1)];
            int depth = parent.Count(c => c == '/');

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = parent;
            }
        }

        return best;
    }

    private static string? FindFileParentDir(string root, string fileName)
    {
        if (!Directory.Exists(root))
            return null;

        string? best = null;
        int bestDepth = int.MaxValue;

        foreach (string file in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
        {
            string? parent = Path.GetDirectoryName(file);

            if (parent == null)
                continue;

            string relative = Path.GetRelativePath(root, parent);
            int depth = relative == "." ? 0 : relative.Count(c => c == Path.DirectorySeparatorChar) + 1;

            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = parent;
            }
        }

        return best;
    }

    public PatchFolderFileSource? CreateRomfsSource(string folderName)
    {
        if (!HasSource)
            return null;

        if (_archive != null)
        {
            string? prefix = ArchivePatchFolderResolver.FindSubDir(_archive.EntryPaths, folderName);

            return prefix == null ? null : PatchFolderFileSource.ForArchive(_archive, prefix);
        }

        string? dir = PatchFolderResolver.FindSubDir(_diskPath!, folderName);

        return dir == null ? null : PatchFolderFileSource.ForFolder(dir);
    }

    public void Dispose() => _archive?.Dispose();
}