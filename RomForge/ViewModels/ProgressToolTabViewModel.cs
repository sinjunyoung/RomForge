using Common.Models;
using Common.Localization;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels;

public abstract class ProgressToolTabViewModel : ToolTabViewModel
{
    private bool _hasCustomProgressLabel;
    private bool _hasCustomProgressTime;

    protected ProgressToolTabViewModel()
    {
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Current))
            {
                if (!_hasCustomProgressLabel)
                    OnPropertyChanged(nameof(ProgressLabel));

                if (!_hasCustomProgressTime)
                    OnPropertyChanged(nameof(ProgressTime));
            }
        };
    }

    public int ProgressPct
    {
        get;
        set
        {
            SetProperty(ref field, value);
            MainViewModel.SetTaskbarProgress(field);
        }
    }

    public string ProgressLabel
    {
        get => _hasCustomProgressLabel ? field : Loc.S.StatusWaiting;
        set
        {
            _hasCustomProgressLabel = true;
            SetProperty(ref field, value);
        }
    } = string.Empty;

    public string ProgressPercent { get; set { SetProperty(ref field, value); } } = string.Empty;

    public string ProgressTime
    {
        get => _hasCustomProgressTime ? field : Loc.S.ProgressElapsed("00:00");
        set
        {
            _hasCustomProgressTime = true;
            SetProperty(ref field, value);
        }
    } = string.Empty;

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