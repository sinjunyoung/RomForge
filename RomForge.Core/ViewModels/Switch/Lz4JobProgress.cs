using Common.Localization;
using Common.Models;

namespace RomForge.Core.ViewModels.Switch;

public sealed class Lz4JobProgress(bool isBuild, bool hasNsz)
{
    private readonly int _stageCount = hasNsz ? 4 : 3;

    private int _stage;
    private int _last;
    private int _max;

    public string Label { get; private set; } = isBuild ? Loc.S.ProgressPreparing : Loc.S.LogStatusCompressing;

    public int Update(int percent, ProgressStage stage)
    {
        if (isBuild)
            UpdateLabel(stage);

        if (percent < _last)
            _stage++;

        _last = percent;

        int value = isBuild ? (Math.Min(_stage, _stageCount - 1) * 100 + percent) / _stageCount : percent;

        _max = Math.Max(_max, value);

        return _max;
    }

    private void UpdateLabel(ProgressStage stage)
    {
        switch (stage)
        {
            case ProgressStage.Hash:
                Label = Loc.S.ProgressHashing;
                break;
            case ProgressStage.Nsz:
                Label = Loc.S.ProgressCompressingNsz;
                break;
            case ProgressStage.Compress:
                Label = Loc.S.LogStatusCompressing;
                break;
        }
    }
}