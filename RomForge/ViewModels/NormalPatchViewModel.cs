using Common.WPF.ViewModels;

namespace RomForge.ViewModels;

public class NormalPatchViewModel : ToolTabViewModel
{
    private readonly Core.AppConfig _config;

    private string? _sourcePath;
    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            _sourcePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceLabel));
        }
    }

    private string? _patchPath;
    public string? PatchPath
    {
        get => _patchPath;
        set
        {
            _patchPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatchLabel));
        }
    }
    
    public bool AutoCompress
    {
        get => _config.Patch.AutoCompress;
        set 
        { 
            _config.Patch.AutoCompress = value;
            OnPropertyChanged(nameof(AutoCompress));
        }
    }

    public string SourceLabel => SourcePath ?? "원본 파일을 드래그하거나 클릭하세요";

    public string PatchLabel => PatchPath ?? "패치 파일을 드래그하거나 클릭하세요";

    private int _progress;
    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public NormalPatchViewModel(Core.AppConfig config)
    {
        _config = config;

        _config.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Core.AppConfig.Patch))
                OnPropertyChanged(nameof(AutoCompress));
        };

        _config.Patch.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Core.PatchConfig.AutoCompress))
                OnPropertyChanged(nameof(AutoCompress));
        };
    }

    public void Clear()
    {
        SourcePath = null;
        PatchPath = null;
        Progress = 0;
        AutoCompress = false;
    }
}