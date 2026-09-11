using Patch.Core;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaPatchApplier
{
    private static readonly HashSet<string> PatchExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".xdelta", ".xdelta3", ".ips", ".ups", ".bps", ".ppf", ".aps" };

    public static List<VitaPatchMatch> Match(string decryptedRoot, string patchDir)
    {
        var sourceFiles = Directory.EnumerateFiles(decryptedRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(Path.GetFileNameWithoutExtension, f => f, StringComparer.OrdinalIgnoreCase);

        var patchFiles = Directory.EnumerateFiles(patchDir, "*", SearchOption.AllDirectories)
            .Where(f => PatchExtensions.Contains(Path.GetExtension(f)));

        var matches = new List<VitaPatchMatch>();

        foreach (var patchFile in patchFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(patchFile);

            if (sourceFiles.TryGetValue(baseName, out var sourceFile))
            {
                matches.Add(new VitaPatchMatch { SourceFile = sourceFile, PatchFile = patchFile });
            }
        }

        return matches;
    }

    public static async Task ApplyAllAsync(List<VitaPatchMatch> matches, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var match = matches[i];
            string tempOutput = match.SourceFile + ".patched";

            await UniversalPatcher.ApplyPatchAsync(match.SourceFile, match.PatchFile, tempOutput, ct: ct);

            File.Delete(match.SourceFile);
            File.Move(tempOutput, match.SourceFile);

            progress?.Report((double)(i + 1) / matches.Count);
        }
    }
}