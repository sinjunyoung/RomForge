using Common.Models;
using Patch.Core.Models;

namespace Patch.Core.Services;

public static class PatchStreamApplier
{
    public static async Task<PatchedContent?> ApplyAsync(Func<Stream> openSource, long sourceLength, PlannedPatch patch, string displayPath, IProgress<ProgressInfo>? progress, Action<string, LogLevel>? log, CancellationToken ct)
    {
        string name = patch.File.DisplayName;

        PatchApplyLog.Start(log, name, displayPath);

        try
        {
            return await StreamPatcher.PrepareAsync(openSource, sourceLength, patch.File.OpenRead, patch.File.Length, PatchApplyLog.Wrap(progress, name), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            PatchApplyLog.Failure(log, patch.Strict, name, displayPath, ex);
            return null;
        }
    }
}