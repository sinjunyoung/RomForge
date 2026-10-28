using Common;

namespace RomForge.Core.Services.Switch;

internal sealed class ScaledProgress(IProgress<ProgressInfo>? inner, int from, int to) : IProgress<ProgressInfo>
{
    public void Report(ProgressInfo value)
    {
        if (inner == null)
            return;

        if (value.Percent < 0)
        {
            inner.Report(value);
            return;
        }

        int percent = from + Math.Clamp(value.Percent, 0, 100) * (to - from) / 100;

        inner.Report(value with { Percent = percent });
    }
}