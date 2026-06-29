using NSW.WPF.ViewModels;

namespace RomForge.ViewModels.Switch;

public class ConverterFileItem : GameFile
{
    private int _progress;
    private string _status = "대기중";
    private string _selectedTargetFormat = string.Empty;

    public ConverterFileItem(string filePath) : base(filePath)
    {
        AvailableFormats = GetAvailableFormats(Extension);
        _selectedTargetFormat = AvailableFormats.FirstOrDefault() ?? string.Empty;
    }

    public int No { get; set; }

    public List<string> AvailableFormats { get; }

    public string SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set { _selectedTargetFormat = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
    }

    public string StatusColor => Status switch
    {
        "완료" => "#4CAF50",
        "실패" => "#F44336",
        "취소" => "#FF9800",
        "변환중" => "#2196F3",
        _ => "#888888"
    };

    private static List<string> GetAvailableFormats(string ext) => ext.ToLower() switch
    {
        "nsp" => ["XCI", "NSZ", "XCZ"],
        "xci" => ["NSP", "NSZ", "XCZ"],
        "nsz" => ["NSP", "XCI", "XCZ"],
        "xcz" => ["XCI", "NSP", "NSZ"],
        _ => []
    };
}