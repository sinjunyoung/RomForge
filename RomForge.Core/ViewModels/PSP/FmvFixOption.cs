using Common.Localization;

namespace RomForge.Core.ViewModels.PSP;

public sealed class FmvFixOption : ViewModelBase
{
    private readonly Func<string> _description;

    public FmvFixOption(uint value, Func<string> description)
    {
        Value = value;
        _description = description;

        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public uint Value { get; }

    public string Label => $"0x{Value:X2} ({_description()})";
}
