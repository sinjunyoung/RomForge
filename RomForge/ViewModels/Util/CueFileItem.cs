using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.ViewModels.Util;

public class CueFileItem(string filePath) : ViewModelBase
{
    private int _progress;
    private string _status = string.Empty;

    public int No { get; set; }

    public string FilePath { get; } = filePath;

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string TargetName => $"{FileName}.cue";

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }
}