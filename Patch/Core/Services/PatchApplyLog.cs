using Common.Localization;

using Common.Models;

namespace Patch.Core.Services;

internal static class PatchApplyLog
{
    public static IProgress<ProgressInfo>? Wrap(IProgress<ProgressInfo>? progress, string name) => progress is null ? null : new Progress<ProgressInfo>(p =>
    {
        if (p.Percent > 0)
            progress.Report(p with { Label = string.IsNullOrEmpty(p.Label) ? Loc.S.ProgressApplyingPatchTo(name) : p.Label });
    });

    public static void Start(Action<string, LogLevel>? log, string name, string displayPath) => log?.Invoke(Loc.S.LogPatchApplyingArrow(name, displayPath), LogLevel.Info);

    public static void Failure(Action<string, LogLevel>? log, bool strict, string name, string displayPath, Exception ex)
    {
        if (strict)
            log?.Invoke($"❌ {Loc.S.LogPatchFailedNamed(name, ex.Message)}", LogLevel.Error);
        else
            log?.Invoke($"⚠️ {Loc.S.LogPatchSourceMismatchSkipped(name, displayPath, ex.Message)}", LogLevel.Info);
    }
}