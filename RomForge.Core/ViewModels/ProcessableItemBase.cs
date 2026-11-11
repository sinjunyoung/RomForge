using Common.Localization;
using RomForge.Core.Interfaces;

namespace RomForge.Core.ViewModels;

public abstract class ProcessableItemBase : ViewModelBase, IProgressTrackable
{
    private ItemStatus _state;
    private string? _statusText;

    protected ProcessableItemBase(ItemStatus initialState = ItemStatus.None)
    {
        _state = initialState;
        StatusLanguageRefresher.Register(this);
    }

    public int No { get; set => SetProperty(ref field, value); }

    public int Progress { get; set => SetProperty(ref field, value); }

    public ItemStatus State
    {
        get => _state;
        set
        {
            if (_state == value && _statusText is null)
                return;

            _state = value;
            _statusText = null;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Status));
            OnStatusChanged();
        }
    }

    public virtual string Status => _statusText ?? Loc.S.GetItemStatus(_state);

    public void SetStatusText(string text)
    {
        _statusText = text;

        OnPropertyChanged(nameof(Status));
        OnStatusChanged();
    }

    internal void RefreshStatus() => OnPropertyChanged(nameof(Status));

    protected virtual void OnStatusChanged() { }
}