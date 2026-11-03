using Common;

namespace Patch.Core.Services;

internal static class PatchApplyLog
{
    public static IProgress<ProgressInfo>? Wrap(IProgress<ProgressInfo>? progress, string name) => progress is null ? null : new Progress<ProgressInfo>(p =>
    {
        if (p.Percent > 0)
            progress.Report(p with { Label = string.IsNullOrEmpty(p.Label) ? $"패치 적용 중 - {name}" : p.Label });
    });

    public static void Start(Action<string, LogLevel>? log, string name, string displayPath) => log?.Invoke($"패치 적용: {name} ➡️ {displayPath}", LogLevel.Info);

    public static void Failure(Action<string, LogLevel>? log, bool strict, string name, string displayPath, Exception ex)
    {
        if (strict)
            log?.Invoke($"❌ 패치 실패 ({name}): {ex.Message}", LogLevel.Error);
        else
            log?.Invoke($"⚠️ 원본이 맞지 않아 건너뜀: {name} ➡️ {displayPath} ({ex.Message})", LogLevel.Info);
    }
}