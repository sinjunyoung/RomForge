using Common.WPF.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomForge.Core;

public class CommonConfig : ViewModelBase
{
    private double _logBoxHeight = 130;
    public double LogBoxHeight
    {
        get => _logBoxHeight;
        set { SetProperty(ref _logBoxHeight, value); }
    }
}

public class WindowConfig : ViewModelBase
{
    private double _left = 100;
    public double Left { get => _left; set => SetProperty(ref _left, value); }

    private double _top = 100;
    public double Top { get => _top; set => SetProperty(ref _top, value); }

    private double _width = 900;
    public double Width { get => _width; set => SetProperty(ref _width, value); }

    private double _height = 715;
    public double Height { get => _height; set => SetProperty(ref _height, value); }

    private bool _isMaximized = false;
    public bool IsMaximized { get => _isMaximized; set => SetProperty(ref _isMaximized, value); }
}

public class PatchConfig : ViewModelBase
{
    private bool _autoCompress;
    public bool AutoCompress
    {
        get => _autoCompress;
        set { SetProperty(ref _autoCompress, value); }
    }
}
public class ChdmanConfig : ViewModelBase
{
    private string _compression = "zlib";
    public string Compression { get => _compression; set => SetProperty(ref _compression, value); }
}

public class SwitchConfig : ViewModelBase
{
    private int _compressLevel = 18;
    public int CompressLevel { get => _compressLevel; set => SetProperty(ref _compressLevel, value); }

    private bool _verifyCompress = false;
    public bool VerifyCompress { get => _verifyCompress; set => SetProperty(ref _verifyCompress, value); }

    private bool _useBlockMode = true;
    public bool UseBlockMode { get => _useBlockMode; set => SetProperty(ref _useBlockMode, value); }

    private bool _useBlocklessMode = false;
    public bool UseBlocklessMode { get => _useBlocklessMode; set => SetProperty(ref _useBlocklessMode, value); }

    private bool _forceKeyGen0 = false;
    public bool ForceKeyGen0 { get => _forceKeyGen0; set => SetProperty(ref _forceKeyGen0, value); }
}

public class AzaharConfig : ViewModelBase
{
    private int _compressLevel = 18;
    public int CompressLevel { get => _compressLevel; set => SetProperty(ref _compressLevel, value); }
}

public class DolphinConfig : ViewModelBase
{
    private int _compressLevel = 18;
    public int CompressLevel { get => _compressLevel; set => SetProperty(ref _compressLevel, value); }
}

public class PS1Config : ViewModelBase
{
    private int _compressLevel = 9;
    public int CompressLevel { get => _compressLevel; set => SetProperty(ref _compressLevel, value); }

    private bool _useGameIdMode = false;
    public bool UseGameIdMode { get => _useGameIdMode; set => SetProperty(ref _useGameIdMode, value); }

    private bool _useFileNameMode = true;
    public bool UseFileNameMode { get => _useFileNameMode; set => SetProperty(ref _useFileNameMode, value); }
}

public class PatchSearchConfig : ViewModelBase
{
    private List<string>? _selectedSystems;
    public List<string>? SelectedSystems { get => _selectedSystems; set => SetProperty(ref _selectedSystems, value); }

    private DateTime? _startDate;
    public DateTime? StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private DateTime? _endDate;
    public DateTime? EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
}

public class TistoryConfig : ViewModelBase
{
    private string? _saveDirectory;
    public string? SaveDirectory { get => _saveDirectory; set => SetProperty(ref _saveDirectory, value); }

    private bool _autoExtractAfterDownload;
    public bool AutoExtractAfterDownload { get => _autoExtractAfterDownload; set => SetProperty(ref _autoExtractAfterDownload, value); }
}

public class OutputFoldersConfig : ViewModelBase
{
    private string? _switchRepackOutputPath;
    public string? SwitchRepackOutputPath { get => _switchRepackOutputPath; set => SetProperty(ref _switchRepackOutputPath, value); }

    private string? _switchMergeOutputPath;
    public string? SwitchMergeOutputPath { get => _switchMergeOutputPath; set => SetProperty(ref _switchMergeOutputPath, value); }

    private string? _wiiURepackOutputPath;
    public string? WiiURepackOutputPath { get => _wiiURepackOutputPath; set => SetProperty(ref _wiiURepackOutputPath, value); }

    private string? _wiiUConvertOutputPath;
    public string? WiiUConvertOutputPath { get => _wiiUConvertOutputPath; set => SetProperty(ref _wiiUConvertOutputPath, value); }

    private string? _threeDsRepackOutputPath;
    public string? ThreeDsRepackOutputPath { get => _threeDsRepackOutputPath; set => SetProperty(ref _threeDsRepackOutputPath, value); }
}

public class AppConfig : ViewModelBase
{
    private static readonly string DefaultFilePath = Path.ChangeExtension(Environment.ProcessPath!, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Lazy<AppConfig> _instance = new(() => new AppConfig().LoadInternal());
    public static AppConfig Instance => _instance.Value;

    private CommonConfig _common = new();
    public CommonConfig Common { get => _common; set => SetProperty(ref _common, value); }

    private WindowConfig _window = new();
    public WindowConfig Window { get => _window; set => SetProperty(ref _window, value); }

    private PatchConfig _patch = new();
    public PatchConfig Patch { get => _patch; set => SetProperty(ref _patch, value); }

    private ChdmanConfig _chdman = new();
    public ChdmanConfig Chdman { get => _chdman; set => SetProperty(ref _chdman, value); }

    private SwitchConfig _switch = new();
    public SwitchConfig Switch { get => _switch; set => SetProperty(ref _switch, value); }

    private AzaharConfig _azahar = new();
    public AzaharConfig Azahar { get => _azahar; set => SetProperty(ref _azahar, value); }

    private DolphinConfig _dolphin = new();
    public DolphinConfig Dolphin { get => _dolphin; set => SetProperty(ref _dolphin, value); }

    private PS1Config _ps1 = new();
    public PS1Config PS1 { get => _ps1; set => SetProperty(ref _ps1, value); }

    private PatchSearchConfig _patchSearch = new();
    public PatchSearchConfig PatchSearch { get => _patchSearch; set => SetProperty(ref _patchSearch, value); }

    private TistoryConfig _tistory = new();
    public TistoryConfig Tistory { get => _tistory; set => SetProperty(ref _tistory, value); }

    private OutputFoldersConfig _outputFolders = new();
    public OutputFoldersConfig OutputFolders { get => _outputFolders; set => SetProperty(ref _outputFolders, value); }

    [JsonConstructor]
    private AppConfig() { }

    private AppConfig LoadInternal()
    {
        if (!File.Exists(DefaultFilePath))
        {
            Save();
            SubscribeToChanges();
            return this;
        }

        try
        {
            var json = File.ReadAllText(DefaultFilePath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json);

            if (loaded != null)
            {
                Common = loaded.Common ?? new();
                Window = loaded.Window ?? new();
                Patch = loaded.Patch ?? new();
                Chdman = loaded.Chdman ?? new();
                Switch = loaded.Switch ?? new();
                Azahar = loaded.Azahar ?? new();
                Dolphin = loaded.Dolphin ?? new();
                PS1 = loaded.PS1 ?? new();
                PatchSearch = loaded.PatchSearch ?? new();
                Tistory = loaded.Tistory ?? new();
                OutputFolders = loaded.OutputFolders ?? new();
            }
        }
        catch
        {
            Save();
        }

        SubscribeToChanges();
        return this;
    }

    private void SubscribeToChanges()
    {
        void AutoSave(object? s, PropertyChangedEventArgs e) => Save();

        Common.PropertyChanged += AutoSave;
        Window.PropertyChanged += AutoSave;
        Chdman.PropertyChanged += AutoSave;
        Switch.PropertyChanged += AutoSave;
        Azahar.PropertyChanged += AutoSave;
        Dolphin.PropertyChanged += AutoSave;
        Patch.PropertyChanged += AutoSave;
        PS1.PropertyChanged += AutoSave;
        PatchSearch.PropertyChanged += AutoSave;
        Tistory.PropertyChanged += AutoSave;
        OutputFolders.PropertyChanged += AutoSave;
    }

    public void Save() => File.WriteAllText(DefaultFilePath, JsonSerializer.Serialize(this, JsonOptions));
}