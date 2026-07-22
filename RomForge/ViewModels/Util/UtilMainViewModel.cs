using System.ComponentModel;

namespace RomForge.ViewModels.Util;

public class UtilMainViewModel : MultiToolTabViewModel
{
    private bool _isAdmin;

    public ZipImageToolMainViewModel ZipImageToolVM { get; } = new();

    public CertsMainViewModel CertsVM { get; } = new();

    public CueMainViewModel CueVM { get; } = new();

    public HashMainViewModel HashVM { get; } = new();

    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            if (_isAdmin == value)
                return;

            _isAdmin = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotAdmin));
        }
    }

    public bool IsNotAdmin => !IsAdmin;

    public UtilMainViewModel()
    {
        IsAdmin = CheckAdmin();

        Tools.Add(HashVM);
        Tools.Add(ZipImageToolVM);
        Tools.Add(CertsVM);
        Tools.Add(CueVM);        

        foreach (var tool in Tools)
            tool.PropertyChanged += Child_PropertyChanged;

        InitializeMultiTools();
    }

    private static bool CheckAdmin()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);

        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void Child_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLocked) || e.PropertyName == nameof(IsIdle))
            OnPropertyChanged(nameof(IsIdle));
    }
}