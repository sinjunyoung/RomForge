using Common;
using Common.WPF.ViewModels;
using RomForge.Core.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace RomForge.ViewModels;

public abstract class ToolTabViewModel : ViewModelBase
{
    private int _lockCount;
    private readonly List<ToolTabViewModel> _tools = [];

    public bool IsLocked => _lockCount > 0;

    public bool IsUnlocked => _lockCount == 0;

    public bool IsIdle => !IsLocked && Tools.All(c => c.IsIdle);

    public List<ToolTabViewModel> Tools => _tools;

    public ICommand? CancelCommand { set; get; }

    public ObservableCollection<LogEntry> LogEntries { get; } = [];

    protected CancellationTokenSource? CancellationTokenSource { get; set; }

    protected void RegisterChild(ToolTabViewModel child)
    {
        child.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsLocked) or nameof(IsIdle))
                OnPropertyChanged(nameof(IsIdle));
        };
    }

    public IDisposable BeginWork()
    {
        Interlocked.Increment(ref _lockCount);
        OnPropertyChanged(nameof(IsLocked));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(IsIdle));
        CommandManager.InvalidateRequerySuggested();

        return new ActionDisposable(() =>
        {
            Interlocked.Decrement(ref _lockCount);
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(IsUnlocked));
            OnPropertyChanged(nameof(IsIdle));
            CommandManager.InvalidateRequerySuggested();
        });
    }

    protected virtual void CleanupTask()
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }

    public void CancelAll()
    {
        foreach (var child in Tools)
        {
            if (child.IsLocked && child.CancelCommand?.CanExecute(null) == true)
                child.CancelCommand.Execute(null);

            if (child.Tools != null)
                child.CancelAll();
        }
    }

    public virtual void Cancel() => CancellationTokenSource?.Cancel();

    public void AppendLog(string msg, LogLevel level = LogLevel.Info) => Application.Current?.Dispatcher?.Invoke(() => LogEntries.Add(new LogEntry { Message = msg, Level = level }));

    public virtual void ClearLog() => Application.Current?.Dispatcher?.Invoke(() => LogEntries.Clear());

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}