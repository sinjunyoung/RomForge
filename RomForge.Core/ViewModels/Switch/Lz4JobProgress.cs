namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress(bool isBuild)
{
    private const int BuildStageCount = 3;

    private int _stage;
    private int _last;
    private int _max;

    public string Label
    {
        get
        {
            if (!isBuild)
                return "압축 중";

            if (_stage == 0)
                return "준비 중";

            return _stage % 2 == 1 ? "압축 중" : "해시 계산 중";
        }
    }

    public int Combine(int percent)
    {
        if (percent < _last)
            _stage++;

        _last = percent;

        int value = isBuild ? (Math.Min(_stage, BuildStageCount - 1) * 100 + percent) / BuildStageCount : percent;

        _max = Math.Max(_max, value);

        return _max;
    }
}