using Common.WPF.ViewModels;
using System.IO;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PS;

public class PbpFileItem : ViewModelBase
{
    private BitmapSource? _icon;
    private string _titleId;
    private string _titleName = string.Empty;
    private string _titleLocalName = string.Empty;
    private List<string> _languages = [];
    private string _status = string.Empty;
    private int _progress;

    public int No { get; set; }

    public string FilePath { get; }

    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();

    public string Directory => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public string FileSize { get; }

    public long FileSizeBytes { get; }

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

    public BitmapSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); }
    }

    public string TitleId
    {
        get => _titleId;
        set { _titleId = value; OnPropertyChanged(); }
    }

    public string TitleName
    {
        get => string.IsNullOrEmpty(_titleName) ? TitleId : _titleName;
        set { _titleName = value; OnPropertyChanged(); }
    }

    public string TitleLocalName
    {
        get => _titleLocalName;
        set { _titleLocalName = value; OnPropertyChanged(); }
    }

    public List<string> Languages
    {
        get => _languages;
        set { _languages = value; OnPropertyChanged(); }
    }

    public PbpFileItem(string filePath)
    {
        FilePath = filePath;
        FileSizeBytes = new FileInfo(filePath).Length;
        FileSize = PickPack.Disk.ETC.FileSize.FormatSize(FileSizeBytes);           
    }
}