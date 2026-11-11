using Common.Localization;

namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress(bool isBuild, bool hasNsz)
{
    private readonly int _stageCount = hasNsz ? 4 : 3;

    private int _stage;
    private int _last;
    private int _max;

    public string Label { get; private set; } = isBuild ? Loc.S.ProgressPreparing : Loc.S.LogStatusCompressing;

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
            Label = Loc.S.ProgressHashing;
        else if (label.Contains("NSZ", StringComparison.OrdinalIgnoreCase))
            Label = Loc.S.ProgressCompressingNsz;
        else if (label.Contains(Common.Localization.Loc.Instance.Current.LabelCompression))
            Label = Loc.S.LogStatusCompressing;
    }
}