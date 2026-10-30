using Patch.Core.Interfaces;
using Patch.Core.Services;
using RomForge.Core.Models.Switch;
using System.IO;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

public sealed class PatchSource
{
    private readonly Dictionary<string, (string Location, int Depth)> _idDirs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string> _toPatchPath;

    public string GenericPath { get; }

    public bool HasGeneric { get; }

    public bool HasIdDirs => _idDirs.Count > 0;

    public IReadOnlyCollection<string> FoundIds => _idDirs.Keys;

    private PatchSource(Func<string, string> toPatchPath, string genericPath, bool hasGeneric)
    {
        _toPatchPath = toPatchPath;
        GenericPath = genericPath;
        HasGeneric = hasGeneric;
    }

    public static bool IsTitleIdName(string name) => name.Length == 16 && name.All(Uri.IsHexDigit);

    public static PatchSource FromFolder(string folder)
    {
        string root = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var source = new PatchSource(location => location, folder, HasGenericFolderContent(root, options));

        if (!Directory.Exists(root))
            return source;

        string selfName = Path.GetFileName(root);

        if (IsTitleIdName(selfName))
        {
            source.AddIdDir(selfName, root, 0);

            if (Path.GetDirectoryName(root) is { Length: > 0 } parent)
            {
                foreach (string sibling in Directory.EnumerateDirectories(parent))
                {
                    string siblingName = Path.GetFileName(sibling);

                    if (IsTitleIdName(siblingName))
                        source.AddIdDir(siblingName, sibling, 0);
                }
            }
        }

        foreach (string dir in Directory.EnumerateDirectories(root, "*", options))
        {
            string name = Path.GetFileName(dir);

            if (IsTitleIdName(name))
                source.AddIdDir(name, dir, Path.GetRelativePath(root, dir).Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar) + 1);
        }

        return source;
    }

    public static PatchSource FromArchive(string archivePath, IArchivePatchSource archive)
    {
        var entries = archive.EntryPaths;
        bool hasGeneric = ArchivePatchFolderResolver.FindSubDir(entries, "exefs") != null || ArchivePatchFolderResolver.FindSubDir(entries, "romfs") != null || entries.Any(IsPatchMarker);
        var source = new PatchSource(dir => ArchivePatchSourceFactory.CombineScope(archivePath, dir + "/"), archivePath, hasGeneric);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string entry in entries)
        {
            string[] parts = entry.Split('/');

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!IsTitleIdName(parts[i]))
                    continue;

                string dir = string.Join('/', parts[..(i + 1)]);

                if (seen.Add(dir))
                    source.AddIdDir(parts[i], dir, i);
            }
        }

        return source;
    }

    public PatchMatch Match(IReadOnlyList<string> ids)
    {
        var covered = ids.Where(id => _idDirs.ContainsKey(id)).ToList();
        var missing = ids.Where(id => !_idDirs.ContainsKey(id)).ToList();
        string? path = covered.Count > 0 ? _toPatchPath(_idDirs[covered[0]].Location) : null;
        return new PatchMatch(path, covered, missing);
    }

    private void AddIdDir(string id, string location, int depth)
    {
        if (!_idDirs.TryGetValue(id, out var existing) || depth < existing.Depth)
            _idDirs[id] = (location, depth);
    }

    private static bool HasGenericFolderContent(string folder, EnumerationOptions options)
    {
        if (!Directory.Exists(folder))
            return false;

        if (PatchFolderResolver.FindSubDir(folder, "exefs") != null || PatchFolderResolver.FindSubDir(folder, "romfs") != null)
            return true;

        return Directory.EnumerateFiles(folder, "*", options).Any(IsPatchMarker);
    }

    private static bool IsPatchMarker(string path) => PatchFileIndex.PatchExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
}