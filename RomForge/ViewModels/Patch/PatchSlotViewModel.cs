using Common.WPF.ViewModels;
using System.IO;

namespace RomForge.ViewModels.Patch;

public class PatchSlotViewModel : ViewModelBase
{
    private string? _filePath;
    private string _title = "패치";

    public string? FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public string Label => Path.GetFileName(FilePath) ?? "패치 파일을 드래그하거나 클릭하세요";
}