using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace RomForge.Core.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;

        OnPropertyChanged(name);

        return true;
    }

    protected bool SetPropertyAndRequery<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        bool result = SetProperty(ref field, value);

        CommandManager.InvalidateRequerySuggested();

        return result;
    }
}