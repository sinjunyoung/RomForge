using Common.Localization;
using System.IO;

namespace RomForge.Core.ViewModels.Patch;

public class PatchSlotViewModel : ViewModelBase
{
    public string? FilePath { get; set { SetProperty(ref field, value); OnPropertyChanged(nameof(Label)); }     }

    public string Title { get; set { SetProperty(ref field, value); } } = Common.Localization.Loc.Instance.Current.HeaderPatch;

    public string Label => Path.GetFileName(FilePath) ?? Loc.S.HintDragOrClickPatchFile;
}