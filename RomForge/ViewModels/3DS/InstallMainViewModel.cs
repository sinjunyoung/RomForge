using Common.WPF.ViewModels;
using RomForge.Core.Models._3DS;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels._3DS;

public class InstallMainViewModel : ToolTabViewModel
{
    public static readonly HashSet<string> SupportedExtensions = [".3ds", ".cia", ".cci", ".zcci"];

    private bool _isInstalling;

    public ObservableCollection<TitleViewModel> Items { get; } = [];

    public Visibility HintVisibility => Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool IsNotInstalling => !IsInstalling;

    public bool IsInstalling
    {
        get => _isInstalling;
        set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotInstalling)); }
    }

    public async Task AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            string ext = Path.GetExtension(fullPath).ToLowerInvariant();

            if (!SupportedExtensions.Contains(ext) || Items.Any(f => f.FilePath == fullPath))
                continue;

            var result = await Task.Run(() => Core.Services._3DS.Util.ParseFile(fullPath));
            var vm = new TitleViewModel()
            {
                Title = result.Title!,
                FilePath = path,
                ProductCode = result.ProductCode,
                ShortDescription = result.ShortDescription,
                Publisher = result.Publisher,
                AvailableLanguages = result.AvailableLanguages,
                Crypto = result.Crypto
            };

            if (result?.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result?.IconPixels, 48 * 4);
                bitmap.Freeze();
                vm.Icon = bitmap;
            }

            Items.Add(vm);
            OnPropertyChanged(nameof(HintVisibility));
        }
    }

    public void RemoveItems(IEnumerable<TitleViewModel> items)
    {
        foreach (var item in items.ToList())
            Items.Remove(item);

        OnPropertyChanged(nameof(HintVisibility));
    }

    public void Clear()
    {
        Items.Clear();
        OnPropertyChanged(nameof(HintVisibility));
    }

    public static string GetFileDialogFilter()
    {
        string wildcards = string.Join(";", SupportedExtensions.Select(ext => $"*{ext}"));

        return $"지원 파일|{wildcards}|모든 파일|*.*";
    }
}