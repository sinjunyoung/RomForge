namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress(bool isBuild, bool hasNsz)
{
    private const int BuildStageCount = 3;

    private int _stage;
    private int _last;
    private int _max;
    private bool _nsz;

    public string Label
    {
        get
        {
            if (_nsz)
                return "NSZ 압축 중";

            if (!isBuild)
                return "압축 중";

            if (_stage == 0)
                return "준비 중";

            return _stage % 2 == 1 ? "압축 중" : "해시 계산 중";
        }
    }

    public void StartNsz()
    {
        _nsz = true;
        _last = 0;
    }

    public int Combine(int percent)
    {
        int value;

        if (_nsz)
            value = 50 + percent / 2;
        else
        {
            if (percent < _last)
                _stage++;

            _last = percent;

            int lz4 = isBuild ? (Math.Min(_stage, BuildStageCount - 1) * 100 + percent) / BuildStageCount : percent;

            value = hasNsz ? lz4 / 2 : lz4;
        }

        _max = Math.Max(_max, value);

        return _max;
    }
}