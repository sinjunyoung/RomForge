using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class PatchLogger
{
    private readonly Action<PatchEntry>? _log;
    private readonly List<PatchEntry> _applied;
    private int _next;

    public PatchLogger(IReadOnlyList<PatchEntry> entries, Action<PatchEntry>? log)
    {
        _log = log;
        _applied = [.. entries.Where(e => e.Applied).OrderBy(e => e.Offset)];

        if (log == null)
            return;

        foreach (var entry in entries.Where(e => !e.Applied))
            log(entry);
    }

    public void Advance(long dataLimit)
    {
        if (_log == null)
            return;

        while (_next < _applied.Count && _applied[_next].Offset < dataLimit)
            _log(_applied[_next++]);
    }

    public void Complete() => Advance(long.MaxValue);
}