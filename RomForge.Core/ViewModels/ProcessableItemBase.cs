using RomForge.Core.Interfaces;

namespace RomForge.Core.ViewModels;

public abstract class ProcessableItemBase(string initialStatus = "") : ViewModelBase, IProgressTrackable
{
    private int _no;
    private int _progress;

    public int No
    {
        get => _no;
        set => SetProperty(ref _no, value);
    }

    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string Status
    {
        get => initialStatus;
        set
        {
            if (SetProperty(ref initialStatus, value))
                OnStatusChanged();
        }
    }

    protected virtual void OnStatusChanged()
    {
    }
}