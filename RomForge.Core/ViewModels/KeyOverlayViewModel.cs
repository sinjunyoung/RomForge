using RomForge.Core.ViewModels.Settings;
using System.Windows;

namespace RomForge.Core.ViewModels;

public class KeyOverlayViewModel : ViewModelBase
{
    private readonly Func<bool> _isAvailable;

    public string IconSource { get; }

    public string Title { get; }

    public string Message { get; }

    public IReadOnlyList<KeyFilePathItemViewModel> Items { get; }

    public Visibility OverlayVisibility => _isAvailable() ? Visibility.Collapsed : Visibility.Visible;

    public KeyOverlayViewModel(string iconSource, string title, string message, IReadOnlyList<KeyFilePathItemViewModel> items, Func<bool> isAvailable)
    {
        IconSource = iconSource;
        Title = title;
        Message = message;
        Items = items;
        _isAvailable = isAvailable;

        foreach (var item in items)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(KeyFilePathItemViewModel.FilePath))
                    Refresh();
            };
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(OverlayVisibility));
}