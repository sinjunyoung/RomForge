using Common.Localization;
using System.IO;

namespace RomForge.Core.ViewModels.Patch;

public class PatchSlotViewModel : ViewModelBase
{
    public PatchSlotViewModel()
    {
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Current))
            {
                OnPropertyChanged(nameof(Label));
                OnPropertyChanged(nameof(Title));
            }
        };
    }

    public string? FilePath
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(Label));
        }
    }
    
    public string Title
    {
        get => field ?? Loc.S.HeaderPatch;
        set => SetProperty(ref field, value);
    }

    public string Label => Path.GetFileName(FilePath) ?? Loc.S.HintDragOrClickPatchFile;
}