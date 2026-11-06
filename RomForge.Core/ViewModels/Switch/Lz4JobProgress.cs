namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress
{
    private const int PhaseCount = 2;

    private int _phase;
    private int _last;
    private int _max;

    public int Combine(int percent)
    {
        if (percent < _last)
            _phase = Math.Min(_phase + 1, PhaseCount - 1);

        _last = percent;
        _max = Math.Max(_max, (_phase * 100 + percent) / PhaseCount);

        return _max;
    }
}