using Common;
using PBP.Core.Models;
using PBP.Core.Services;
using RomForge.Core.Models.PS;
using System.IO;

namespace RomForge.Core.Services.PS;

public record PackingPlan(string TargetOutputPath, string? GameDirectory);

public class PackingJobRunner
{
    public static PackingPlan PlanOutput(string firstDiscPath, string gameTitle, string mainGameId)
    {
        var baseDirectory = Path.GetDirectoryName(firstDiscPath)!;

        string targetOutputPath;
        string? gameDirectory = null;

        if (AppConfig.Instance.PS1.UseGameIdMode)
        {
            gameDirectory = Path.Combine(baseDirectory, mainGameId);
            targetOutputPath = Path.Combine(gameDirectory, "EBOOT.PBP");
        }
        else
        {
            var safeTitle = string.Concat(gameTitle.Split(Path.GetInvalidFileNameChars()));
            var extension = AppConfig.Instance.PS1.UseUpperCase ? ".PBP" : ".pbp";
            targetOutputPath = Path.Combine(baseDirectory, safeTitle + extension);
        }

        targetOutputPath = Utils.GetUniqueFilePath(targetOutputPath);

        return new PackingPlan(targetOutputPath, gameDirectory);
    }

    public static async Task RunAsync(List<DiscFileItem> orderedItems, string gameTitle, string mainGameId, PackingPlan plan, int compressLevel, PbpAssets assets, byte[]? popsConfig, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        var resolvedDiscs = new List<ResolvedDisc>();

        try
        {
            if (plan.GameDirectory != null && !Directory.Exists(plan.GameDirectory))
                Directory.CreateDirectory(plan.GameDirectory);

            var discInfos = new List<DiscWriteInfo>();

            foreach (var item in orderedItems)
            {
                var resolved = RawDiscProcessor.Resolve(item.FilePath);
                resolvedDiscs.Add(resolved);
                discInfos.Add(new DiscWriteInfo(resolved.IsoStream, resolved.IsoLength, item.GameId, orderedItems.Count > 1 ? $"{gameTitle} - Disc {item.No}" : gameTitle, resolved.TocData));
            }

            await PbpPackager.WritePbpAsync(discInfos, mainGameId, gameTitle, plan.TargetOutputPath, compressLevel, assets, popsConfig, progress, ct);
        }
        finally
        {
            foreach (var d in resolvedDiscs)
                d.Dispose();
        }
    }

    public static void CleanupFailedOutput(string? filePath, string? folderPath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            File.Delete(filePath);

        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath) && !Directory.EnumerateFileSystemEntries(folderPath).Any())
            Directory.Delete(folderPath);
    }
}