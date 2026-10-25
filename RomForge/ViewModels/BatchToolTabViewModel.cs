using Common.WPF.Interfaces;
using System.Windows;
using System.Windows.Shell;

namespace RomForge.ViewModels;

public abstract class BatchToolTabViewModel : ToolTabViewModel
{
    private readonly OverallProgressTracker _overall;

    protected BatchToolTabViewModel() => _overall = new OverallProgressTracker(PublishOverall);

    protected void BeginBatch(int total) => _overall.Begin(total);

    protected void StartBatchItem() => _overall.StartItem();

    protected void ReportBatchItem(IProgressTrackable item, int percent)
    {
        if (percent < 0)
            return;

        item.Progress = percent;
        _overall.Report(percent);
    }

    protected void CompleteBatchItem(bool success = true) => _overall.CompleteItem(success);

    protected void EndBatch(bool canceled = false, bool failed = false)
    {
        if (!canceled && (failed || _overall.HasFailures))
            MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
        else
            MainViewModel.SetTaskbarProgress(0);

        base.CleanupTask();
    }

    protected virtual void OnOverallChanged(int percent) { }

    private void PublishOverall(int percent)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            ApplyOverall(percent);
        else
            dispatcher.BeginInvoke(new Action(() => ApplyOverall(percent)));
    }

    private void ApplyOverall(int percent)
    {
        MainViewModel.SetTaskbarProgress(percent);
        OnOverallChanged(percent);
    }
}