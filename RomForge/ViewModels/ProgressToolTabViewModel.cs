using Common.Models;
using Common.Localization;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels;

public abstract class ProgressToolTabViewModel : ToolTabViewModel
{
    public int ProgressPct { get; set { SetProperty(ref field, value); MainViewModel.SetTaskbarProgress(field); } }

    public string ProgressLabel { get; set { SetProperty(ref field, value); } } = Loc.S.StatusWaiting;

    public string ProgressPercent { get; set { SetProperty(ref field, value); } } = string.Empty;

    public string ProgressTime { get; set { SetProperty(ref field, value); } } = Loc.S.ProgressElapsed("00:00");

    public string ProgressSpeed { get; set { SetProperty(ref field, value); } } = string.Empty;

    protected Progress<ProgressInfo> BuildProgressReporter() =>
        new(info =>
        {
            ProgressPct = info.Percent;
            ProgressLabel = info.Label;
            ProgressPercent = $"{info.Percent}%";
            ProgressTime = info.TimeInfo;
            ProgressSpeed = info.Speed;
        });

    protected override void CleanupTask()
    {
        base.CleanupTask();

        ProgressPct = 0;
        ProgressLabel = string.Empty;
        ProgressPercent = "0%";
        ProgressTime = string.Empty;
        ProgressSpeed = string.Empty;
    }
}