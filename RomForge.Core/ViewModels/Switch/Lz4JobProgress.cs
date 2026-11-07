namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress(bool isBuild, bool hasNsz)
{
    private readonly int _stageCount = hasNsz ? 4 : 3;

    private int _stage;
    private int _last;
    private int _max;

    public string Label { get; private set; } = isBuild ? "준비 중" : "압축 중";

    public int Update(int percent, string? label)
    {
        if (isBuild)
            UpdateLabel(label);

        if (percent < _last)
            _stage++;

        _last = percent;

        int value = isBuild ? (Math.Min(_stage, _stageCount - 1) * 100 + percent) / _stageCount : percent;

        _max = Math.Max(_max, value);

        return _max;
    }

    private void UpdateLabel(string? label)
    {
        if (string.IsNullOrEmpty(label))
            return;

        if (label.Contains("해시"))
            Label = "해시 계산 중";
        else if (label.Contains("NSZ", StringComparison.OrdinalIgnoreCase))
            Label = "NSZ 압축 중";
        else if (label.Contains("압축"))
            Label = "압축 중";
    }
}