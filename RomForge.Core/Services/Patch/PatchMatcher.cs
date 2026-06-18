using RomForge.Core.Models.Patch;
using System.IO;
using System.IO.Compression;

namespace RomForge.Core.Services.Patch;

public static class PatchMatcher
{
    private static readonly HashSet<string> PatchExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".xdelta", ".xdelta3", ".ips", ".ups", ".bps", ".ppf", ".aps" };

    public static List<PatchPair> Match(string sourcePath, string patchPath)
    {
        var sources = ExpandSource(sourcePath);
        var patches = ExpandPatch(patchPath);
        var patchMap = patches.ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => p, StringComparer.OrdinalIgnoreCase);
        var sourceMap = sources.ToDictionary(s => Path.GetFileNameWithoutExtension(s), s => s, StringComparer.OrdinalIgnoreCase);
        var pairs = new List<PatchPair>();
        var usedPatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (baseName, srcPath) in sourceMap)
        {
            if (patchMap.TryGetValue(baseName, out var patchFile))
            {
                pairs.Add(new PatchPair
                {
                    BaseName   = baseName,
                    SourcePath = srcPath,
                    PatchPath  = patchFile,
                    Status     = PairStatus.Matched
                });
                usedPatches.Add(patchFile);
            }
            else
            {
                pairs.Add(new PatchPair
                {
                    BaseName   = baseName,
                    SourcePath = srcPath,
                    Status     = PairStatus.OrphanSource
                });
            }
        }

        foreach (var (baseName, patchFile) in patchMap)
        {
            if (!usedPatches.Contains(patchFile))
            {
                pairs.Add(new PatchPair
                {
                    BaseName  = baseName,
                    PatchPath = patchFile,
                    Status    = PairStatus.OrphanPatch
                });
            }
        }

        return pairs;
    }

    private static List<string> ExpandSource(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".zip")
        {
            using var zip = ZipFile.OpenRead(path);

            return [.. zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => $"{path}|{e.FullName}")];
        }

        return [path];
    }

    private static List<string> ExpandPatch(string path)
    {
        if (Directory.Exists(path))
        {
            return [.. Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Where(f => PatchExtensions.Contains(Path.GetExtension(f)))];
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".zip")
        {
            using var zip = ZipFile.OpenRead(path);

            return [.. zip.Entries
                .Where(e => PatchExtensions.Contains(Path.GetExtension(e.Name)))
                .Select(e => $"{path}|{e.FullName}")];
        }

        return [path];
    }
}
