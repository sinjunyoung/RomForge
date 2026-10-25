using PSP.PBP.Models;
using PSP.PBP.Services;
using RomForge.Core.Models.PSP;
using System.IO;

namespace RomForge.Core.Services.PSP;

public class DiscImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cue", ".m3u", ".iso", ".chd", ".bin" };

    public async Task<ImportResult> ImportAsync(IEnumerable<string> inputPaths, IEnumerable<string> existingPaths, int roomAvailable)
    {
        var rawFiles = ExpandPaths(inputPaths).ToList();
        var explodedPaths = new List<string>();

        foreach (var file in rawFiles)
        {
            if (Path.GetExtension(file).Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                explodedPaths.AddRange(ResolveM3uAllDiscs(file));
            else
                explodedPaths.Add(file);
        }

        var existing = existingPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newCandidates = new List<string>();

        foreach (var p in explodedPaths)
        {
            var targetPath = p;

            if (Path.GetExtension(p).Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                var cuePath = Path.ChangeExtension(p, ".cue");
                if (File.Exists(cuePath))
                {
                    targetPath = cuePath;
                }
            }

            if (!SupportedExtensions.Contains(Path.GetExtension(targetPath)))
                continue;

            if (existing.Add(targetPath))
                newCandidates.Add(targetPath);
        }

        var toValidate = newCandidates.Take(Math.Max(roomAvailable, 0)).ToList();
        var overLimitSkipped = newCandidates.Count - toValidate.Count;
        var results = await Task.WhenAll(toValidate.Select(ValidateCandidateAsync));
        var imported = new List<ImportedDiscFile>();
        var failures = new List<ImportFailure>();

        foreach (var (disc, failedPath, reason) in results)
        {
            if (disc != null)
                imported.Add(disc);
            else
                failures.Add(new ImportFailure(Path.GetFileName(failedPath!), reason!));
        }

        return new ImportResult(imported, overLimitSkipped, failures);
    }

    private async Task<(ImportedDiscFile? Disc, string? FailedPath, string? Reason)> ValidateCandidateAsync(string path)
    {
        try
        {
            return await Task.Run<(ImportedDiscFile?, string?, string?)>(() =>
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var size = DiscSizeResolver.GetTotalSize(path);
                string gameId;

                if (ext == ".chd")
                    gameId = GameIdReader.ReadFromDisk(DiskSource.FromChd(path));
                else if (ext == ".cue")
                {
                    var binPath = CueFileResolver.GetBinPath(path);
                    using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read);

                    gameId = GameIdReader.ReadFromStream(stream, stream.Length);
                }
                else
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

                    gameId = GameIdReader.ReadFromStream(fs, fs.Length);
                }

                var presetConfig = PsarPackager.GetPopsConfig(gameId);

                return (new ImportedDiscFile(path, gameId, size, presetConfig), null, null);
            });
        }
        catch (Exception ex)
        {
            return (null, path, ex.Message);
        }
    }

    private static List<string> ResolveM3uAllDiscs(string m3uPath)
    {
        var dir = Path.GetDirectoryName(m3uPath)!;
        var paths = new List<string>();

        if (!File.Exists(m3uPath))
            return paths;

        var lines = File.ReadAllLines(m3uPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'));

        foreach (var line in lines)
        {
            var fullPath = Path.IsPathRooted(line) ? line : Path.Combine(dir, line);
            paths.Add(fullPath);
        }

        return paths;
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }

    public static string GetFileDialogFilter()
    {
        var wildcards = string.Join(";", SupportedExtensions.Select(ext => $"*{ext}"));

        return $"지원 파일|{wildcards}|모든 파일|*.*";
    }
}