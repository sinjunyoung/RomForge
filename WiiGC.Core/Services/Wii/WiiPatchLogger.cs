using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiPatchLogger
{
    private readonly Action<WiiPatchEntry>? _log;
    private readonly List<WiiPatchEntry> _applied;
    private int _next;

    public WiiPatchLogger(IReadOnlyList<WiiPatchEntry> entries, Action<WiiPatchEntry>? log)
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