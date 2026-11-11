using Common.Localization;

using RomForge.Core.ViewModels.Switch;

namespace RomForge.Core.Models.Switch;

public class ConverterFileItem : GameFile, Interfaces.IConvertible
{
    public ConverterFileItem(string filePath) : base(filePath)
    {
        AvailableFormats = GetAvailableFormats(Extension);
        SelectedTargetFormat = AvailableFormats.FirstOrDefault() ?? string.Empty;
    }

    public List<string> AvailableFormats { get; }

    public string SelectedTargetFormat { get; set => SetProperty(ref field, value); }

    public string StatusColor => State switch
    {
        ItemStatus.Done => "#4CAF50",
        ItemStatus.Failed => "#F44336",
        ItemStatus.Cancelled => "#FF9800",
        ItemStatus.Converting => "#2196F3",
        _ => "#888888"
    };

    protected override void OnStatusChanged()
    {
        base.OnStatusChanged();
        OnPropertyChanged(nameof(StatusColor));
    }

    private static List<string> GetAvailableFormats(string ext) => ext.ToLowerInvariant() switch
    {
        "nsp" => ["NSZ", "XCI", "XCZ"],
        "xci" => ["XCZ", "NSP", "NSZ"],
        "nsz" => ["NSP", "XCI", "XCZ"],
        "xcz" => ["XCI", "NSP", "NSZ"],
        _ => []
    };
}