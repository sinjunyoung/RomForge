using Common.WPF.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

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

public class AppConfig : ViewModelBase
{
    private static readonly string DefaultFilePath = Path.ChangeExtension(Environment.ProcessPath!, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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

    public CommonConfig Common { get; set; } = new();

    public AppConfig Load()
    {
        if (!File.Exists(DefaultFilePath)) { Save(); return this; }
        try
        {
            var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(DefaultFilePath));
            if (loaded != null)
            {
                Common = loaded.Common ?? new();
                Patch = loaded.Patch ?? new();
                Chdman = loaded.Chdman ?? new();
                Switch = loaded.Switch ?? new();
                Azahar = loaded.Azahar ?? new();
                Dolphin = loaded.Dolphin ?? new();                
                PS1 = loaded.PS1 ?? new();
            }

            SubscribeToChanges();
        }
        catch { Save(); }
        return this;
    }

    private void SubscribeToChanges()
    {
        void AutoSave(object? s, PropertyChangedEventArgs e) => Save();

        Common.PropertyChanged += AutoSave;
        Chdman.PropertyChanged += AutoSave;
        Switch.PropertyChanged += AutoSave;
        Azahar.PropertyChanged += AutoSave;
        Dolphin.PropertyChanged += AutoSave;
        Patch.PropertyChanged += AutoSave;
        PS1.PropertyChanged += AutoSave;
    }

    public void Save() => File.WriteAllText(DefaultFilePath, JsonSerializer.Serialize(this, JsonOptions));
}