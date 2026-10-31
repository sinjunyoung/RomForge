using RomForge.Core.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomForge.Core;

public class CommonConfig : ViewModelBase
{
    public double LogBoxHeight { get; set { SetProperty(ref field, value); } } = 130;

    public bool Topmost { get; set { SetProperty(ref field, value); } }
    
    public bool AutoCheckUpdate { get; set { SetProperty(ref field, value); } } = true;

    public string? Language { get; set { SetProperty(ref field, value); } } = "Korean";

    public bool IsMuted { get; set { SetProperty(ref field, value); } }

    public double SoundVolume { get; set { SetProperty(ref field, value); } } = 50;

    public string CompleteSoundPath { get; set { SetProperty(ref field, value); } } = "complete.mp3";

    public string CancelSoundPath { get; set { SetProperty(ref field, value); } } = "cancel.mp3";

    public string ErrorSoundPath { get; set { SetProperty(ref field, value); } } = "error.mp3";
}

public class WindowConfig : ViewModelBase
{
    public double Left { get; set { SetProperty(ref field, value); } } = 100;

    public double Top { get; set { SetProperty(ref field, value); } } = 100;

    public double Width { get; set { SetProperty(ref field, value); } } = 900;

    public double Height { get; set { SetProperty(ref field, value); } } = 715;

    public bool IsMaximized { get; set { SetProperty(ref field, value); } } = false;
}

public class PatchConfig : ViewModelBase
{
    public bool AutoCompress { get; set { SetProperty(ref field, value); } }

    public bool NamingEnabled { get; set { SetProperty(ref field, value); } } = true;

    public string NamingFormat { get; set { SetProperty(ref field, value); } } = "{fileName} (v{Version}_{Date:yyMMdd})";

    public bool UseNormalPatchCustomOutputPath { get; set { SetProperty(ref field, value); } }

    public string? OutputPath { get; set { SetProperty(ref field, value); } }
}

public class ChdmanConfig : ViewModelBase
{
    public string Compression { get; set { SetProperty(ref field, value); } } = "zlib";
}

public class SwitchConfig : ViewModelBase
{
    public int CompressLevel { get; set { SetProperty(ref field, value); } } = 18;

    public int Lz4CompressLevel { get; set { SetProperty(ref field, value); } } = 9;

    public bool VerifyCompress { get; set { SetProperty(ref field, value); } } = false;

    public bool UseBlockMode { get; set { SetProperty(ref field, value); } } = true;

    public bool UseBlocklessMode { get; set { SetProperty(ref field, value); } } = false;

    public bool ForceKeyGen0 { get; set { SetProperty(ref field, value); } } = false;
}

public class AzaharConfig : ViewModelBase
{
    public int CompressLevel { get; set { SetProperty(ref field, value); } } = 18;
}

public class WiiGCConfig : ViewModelBase
{
    public int CompressLevel { get; set { SetProperty(ref field, value); } } = 18;
}

public class PS1Config : ViewModelBase
{
    public int CompressLevel { get; set { SetProperty(ref field, value); } } = 9;

    public bool UseGameIdMode { get; set { SetProperty(ref field, value); } } = false;

    public bool UseFileNameMode { get; set { SetProperty(ref field, value); } } = true;

    public bool UseUpperCase { get; set { SetProperty(ref field, value); } } = false;

    public bool IsHighQualityMode { get; set { SetProperty(ref field, value); } } = true;
}

public class TistoryConfig : ViewModelBase
{
    public string? SaveDirectory { get; set { SetProperty(ref field, value); } }

    public bool AutoExtractAfterDownload { get; set { SetProperty(ref field, value); } }
}

public class OutputFoldersConfig : ViewModelBase
{
    public string? SwitchRepackOutputPath { get; set { SetProperty(ref field, value); } }

    public string? SwitchMergeOutputPath { get; set { SetProperty(ref field, value); } }

    public string? SwitchSplitOutputPath { get; set { SetProperty(ref field, value); } }

    public string? WiiURepackOutputPath { get; set { SetProperty(ref field, value); } }

    public string? WiiUConvertOutputPath { get; set { SetProperty(ref field, value); } }

    public string? ThreeDsRepackOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaOutputPath { get; set { SetProperty(ref field, value); } }

    public string? VitaConvertOutputPath { get; set { SetProperty(ref field, value); } }

    public bool UseCompressCustomOutputPath { get; set { SetProperty(ref field, value); } }

    public string? CompressOutputPath { get; set { SetProperty(ref field, value); } }

    public bool UseConvertCustomOutputPath { get; set { SetProperty(ref field, value); } }

    public string? ConvertOutputPath { get; set { SetProperty(ref field, value); } }

    public string? WiiOutputPath { get; set { SetProperty(ref field, value); } }

    public string? SwitchLz4MergeOutputPath { get; set { SetProperty(ref field, value); } }
}

public class AppConfig : ViewModelBase
{
    private static readonly string DefaultFilePath = Path.ChangeExtension(Environment.ProcessPath!, "config.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Lazy<AppConfig> _instance = new(() => new AppConfig().LoadInternal());
    public static AppConfig Instance => _instance.Value;

    public CommonConfig Common { get; set { SetProperty(ref field, value); } } = new();

    public WindowConfig Window { get; set { SetProperty(ref field, value); } } = new();

    public PatchConfig Patch { get; set { SetProperty(ref field, value); } } = new();

    public ChdmanConfig Chdman { get; set { SetProperty(ref field, value); } } = new();

    public SwitchConfig Switch { get; set { SetProperty(ref field, value); } } = new();

    public AzaharConfig Azahar { get; set { SetProperty(ref field, value); } } = new();

    public WiiGCConfig WiiGC { get; set { SetProperty(ref field, value); } } = new();

    public PS1Config PS1 { get; set { SetProperty(ref field, value); } } = new();

    public TistoryConfig Tistory { get; set { SetProperty(ref field, value); } } = new();

    public OutputFoldersConfig OutputFolders { get; set { SetProperty(ref field, value); } } = new();

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
                WiiGC = loaded.WiiGC ?? new();
                PS1 = loaded.PS1 ?? new();
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
        WiiGC.PropertyChanged += AutoSave;
        Patch.PropertyChanged += AutoSave;
        PS1.PropertyChanged += AutoSave;
        Tistory.PropertyChanged += AutoSave;
        OutputFolders.PropertyChanged += AutoSave;
    }

    public void Save() => File.WriteAllText(DefaultFilePath, JsonSerializer.Serialize(this, JsonOptions));
}