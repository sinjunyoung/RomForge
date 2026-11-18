using Common.Localization;
using RomForge.Core.ViewModels;

namespace RomForge.ViewModels.Settings;

public sealed class ChdmanCompressionOption : ViewModelBase
{
    private readonly Func<string> _display;

    public ChdmanCompressionOption(string value, Func<string> display)
    {
        Value = value;
        _display = display;

        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Display));
    }

    public string Value { get; }

    public string Display => _display();
}
