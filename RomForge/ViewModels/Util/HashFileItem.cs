using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.ViewModels.Util;

public class HashFileItem : ViewModelBase
{
    private int _progress;
    private string _status = string.Empty;
    private string _hashResult = string.Empty;

    public int No { get; set; }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public string FileSize { get; }

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

    public string HashResult
    {
        get => _hashResult;
        set { _hashResult = value; OnPropertyChanged(); }
    }

    public HashFileItem(string filePath)
    {
        FilePath = filePath;
        var info = new FileInfo(filePath);
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(info.Exists ? info.Length : 0);
    }
}