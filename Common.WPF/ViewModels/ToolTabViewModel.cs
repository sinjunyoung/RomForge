using System.Windows.Input;

namespace Common.WPF.ViewModels;

public abstract class ToolTabViewModel : ViewModelBase
{
    private int _lockCount;
    private readonly List<ToolTabViewModel> _tools = [];

    public bool IsLocked => _lockCount > 0;

    public bool IsUnlocked => _lockCount == 0;

    public bool IsIdle => !IsLocked && Tools.All(c => c.IsIdle);

    public List<ToolTabViewModel> Tools => _tools;

    public ICommand? CancelCommand { set;  get; }

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

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}