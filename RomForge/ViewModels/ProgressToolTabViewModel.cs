using Common;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels;

public abstract class ProgressToolTabViewModel : ToolTabViewModel
{
    private int _progressPct;
    public int ProgressPct
    {
        get => _progressPct;
        set
        {
            _progressPct = value;

            MainViewModel.SetTaskbarProgress(_progressPct);
            OnPropertyChanged();
        }
    }

    private string _progressLabel = "대기 중...";
    public string ProgressLabel
    {
        get => _progressLabel;
        set { _progressLabel = value; OnPropertyChanged(); }
    }

    private string _progressPercent = string.Empty;
    public string ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    private string _progressTime = "00:00 경과";
    public string ProgressTime
    {
        get => _progressTime;
        set { _progressTime = value; OnPropertyChanged(); }
    }

    private string _progressSpeed = string.Empty;
    public string ProgressSpeed
    {
        get => _progressSpeed;
        set { _progressSpeed = value; OnPropertyChanged(); }
    }

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