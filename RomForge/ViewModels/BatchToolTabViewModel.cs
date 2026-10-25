using Common.WPF.Interfaces;
using System.Windows;
using System.Windows.Shell;

namespace RomForge.ViewModels;

public abstract class BatchToolTabViewModel : ToolTabViewModel
{
    private readonly OverallProgressTracker _overall;

    protected BatchToolTabViewModel() => _overall = new OverallProgressTracker(PublishTaskbar);

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

    protected void EndBatch(bool canceled = false)
    {
        if (!canceled && _overall.HasFailures)
            MainViewModel.SetTaskbarProgress(100, TaskbarItemProgressState.Error);
        else
            MainViewModel.SetTaskbarProgress(0);

        base.CleanupTask();
    }

    private static void PublishTaskbar(int percent)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
            MainViewModel.SetTaskbarProgress(percent);
        else
            dispatcher.BeginInvoke(new Action(() => MainViewModel.SetTaskbarProgress(percent)));
    }
}