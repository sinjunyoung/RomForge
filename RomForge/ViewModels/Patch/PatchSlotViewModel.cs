using RomForge.Core.ViewModels;
using System.IO;

namespace RomForge.ViewModels.Patch;

public class PatchSlotViewModel : ViewModelBase
{
    public string? FilePath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(Label)); }     }

    public string Title { get; set { SetProperty(ref field, value); } } = "패치";

    public string Label => Path.GetFileName(FilePath) ?? "패치 파일을 드래그하거나 클릭하세요";
}