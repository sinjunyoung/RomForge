using Common;

namespace Patch.Core.Services;

public sealed class PatchSession : IDisposable
{
    private readonly PatchSource _source;

    public PatchPlan Plan { get; }

    private PatchSession(PatchSource source, PatchPlan plan)
    {
        _source = source;
        Plan = plan;
    }

    public static PatchSession Open(string patchPath, string[] anchors, IEnumerable<string> targetPaths, Action<string, LogLevel>? log = null, string? label = null)
    {
        var source = PatchSource.Open(patchPath, anchors);

        try
        {
            var plan = PatchPlan.Build(source.Index, targetPaths);

            plan.LogSummary(log, label);

            return new PatchSession(source, plan);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public Stream OpenPatched(string path, Func<Stream> openOriginal, long originalLength, Action<string, LogLevel>? log, CancellationToken ct) => PatchApplier.OpenPatched(path, Plan, openOriginal, originalLength, log, ct);

    public byte[] ApplyPatches(string path, Func<Stream> openOriginal, long originalLength, Action<string, LogLevel>? log, CancellationToken ct) => PatchApplier.ApplyPatches(path, Plan, openOriginal, originalLength, log, ct);

    public void Dispose() => _source.Dispose();
}