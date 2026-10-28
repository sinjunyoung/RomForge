using RomForge.Core.ViewModels.Switch;

namespace RomForge.Core.Models.Switch;

public class ConverterFileItem : GameFile, Interfaces.IConvertible
{
    public const string Lz4NspFormat = "NSP (Lz4)";

    private string _selectedTargetFormat;

    public ConverterFileItem(string filePath) : base(filePath)
    {
        AvailableFormats = GetAvailableFormats(Extension);
        _selectedTargetFormat = AvailableFormats.FirstOrDefault() ?? string.Empty;
    }

    public List<string> AvailableFormats { get; }

    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set => SetProperty(ref _selectedTargetFormat, value);
    }

    public string StatusColor => Status switch
    {
        "완료" => "#4CAF50",
        "실패" => "#F44336",
        "취소" => "#FF9800",
        "변환중" => "#2196F3",
        _ => "#888888"
    };

    protected override void OnStatusChanged()
    {
        base.OnStatusChanged();
        OnPropertyChanged(nameof(StatusColor));
    }

    private static List<string> GetAvailableFormats(string ext) => ext.ToLowerInvariant() switch
    {
        "nsp" => ["NSZ", "XCI", "XCZ", Lz4NspFormat],
        "xci" => ["XCZ", "NSP", "NSZ"],
        "nsz" => ["NSP", "XCI", "XCZ"],
        "xcz" => ["XCI", "NSP", "NSZ"],
        _ => []
    };
}