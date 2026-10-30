using RomForge.Core.Interfaces;

namespace RomForge.Core.ViewModels;

public abstract class ProcessableItemBase(string initialStatus = "") : ViewModelBase, IProgressTrackable
{

    public int No { get; set => SetProperty(ref field, value); }

    public int Progress { get; set => SetProperty(ref field, value); }

    public string Status
    {
        get => initialStatus;
        set
        {
            if (SetProperty(ref initialStatus, value))
                OnStatusChanged();
        }
    }

    protected virtual void OnStatusChanged() { }
}