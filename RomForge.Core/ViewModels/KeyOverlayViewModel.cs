using RomForge.Core.ViewModels.Settings;
using System.Windows;
using Common.Localization;

namespace RomForge.Core.ViewModels;

public class KeyOverlayViewModel : ViewModelBase
{
    private readonly Func<bool> _isAvailable;
    private readonly Func<string> _titleProvider;
    private readonly Func<string> _messageProvider;

    public string IconSource { get; }

    public string Title => _titleProvider();

    public string Message => _messageProvider();

    public IReadOnlyList<KeyFilePathItemViewModel> Items { get; }

    public Visibility OverlayVisibility => _isAvailable() ? Visibility.Collapsed : Visibility.Visible;

    public KeyOverlayViewModel(string iconSource, Func<string> titleProvider, Func<string> messageProvider, IReadOnlyList<KeyFilePathItemViewModel> items, Func<bool> isAvailable)
    {
        IconSource = iconSource;
        _titleProvider = titleProvider;
        _messageProvider = messageProvider;
        Items = items;
        _isAvailable = isAvailable;

        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Current))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Message));
            }
        };

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