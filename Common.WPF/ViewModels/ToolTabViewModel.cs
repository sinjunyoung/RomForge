using System.Windows.Input;

namespace Common.WPF.ViewModels;

public abstract class ToolTabViewModel : ViewModelBase
{
    private int _lockCount;
    private readonly List<ToolTabViewModel> _children = [];

    public bool IsLocked => _lockCount > 0;
    public bool IsUnlocked => _lockCount == 0;
    public bool IsIdle => !IsLocked && Children.All(c => c.IsIdle);

    public List<ToolTabViewModel> Children => _children;

    protected void RegisterChild(ToolTabViewModel child)
    {
        Children.Add(child);
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
    private sealed class ActionDisposable(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}