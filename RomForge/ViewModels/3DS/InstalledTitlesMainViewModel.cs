using RomForge.Core.Models._3DS;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using N3DS.Core.Enums;
using N3DS.Core.Models;
using N3DS.Core.FileSystem;
using N3DS.Core.Crypto;
using N3DS.Core.Services;
using N3DS.Core.IO;

namespace RomForge.ViewModels._3DS;

public class InstalledTitlesMainViewModel(Action<string> setStatus) : ToolTabViewModel
{
    private string _filterType = "All";
    private string _searchQuery = string.Empty;
    private bool _isLoading;

    public ObservableCollection<TitleViewModel> AllTitles { get; } = [];

    public ObservableCollection<TitleViewModel> FilteredTitles { get; } = [];

    public Visibility LoadingVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !_isLoading && AllTitles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ListVisibility => !_isLoading && AllTitles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SearchHintVisibility => string.IsNullOrEmpty(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(KeyStore keyStore, SdCrypto sdCrypto, SdTitleScanner scanner, Action<int, int> onProgress, CancellationToken ct = default)
    {
        SetLoading(true);
        AllTitles.Clear();
        FilteredTitles.Clear();

        setStatus("타이틀 스캔 중...");

        var titles = await Task.Run(() =>
            scanner.ScanTitles(new Progress<string>(msg => setStatus(msg))), ct);

        setStatus($"메타데이터 로딩 중... (총 {titles.Count}개)");

        for (int i = 0; i < titles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var vm = await CreateViewModel(titles[i], scanner, sdCrypto, keyStore);
            AllTitles.Add(vm);
            onProgress(i + 1, titles.Count);
        }

        RefreshFilter(_searchQuery);
        SetLoading(false);
        setStatus($"{titles.Count}개 타이틀 로드 완료");
    }

    public void SetFilter(string filterType, string query)
    {
        _filterType = filterType;
        RefreshFilter(query);
    }

    public void RefreshFilter(string query)
    {
        _searchQuery = query;
        OnPropertyChanged(nameof(SearchHintVisibility));

        FilteredTitles.Clear();

        var filtered = AllTitles.AsEnumerable();

        filtered = _filterType switch
        {
            "Game" => filtered.Where(t => t.Title.Type is TitleType.Application or TitleType.SystemApplication),
            "Update" => filtered.Where(t => t.Title.Type == TitleType.Patch),
            "Dlc" => filtered.Where(t => t.Title.Type == TitleType.DlcContent),
            _ => filtered,
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.ToLowerInvariant();

            filtered = filtered.Where(t =>
                (t.ShortDescription?.Contains(q, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (t.Publisher?.Contains(q, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                t.TitleId.Contains(q, StringComparison.InvariantCultureIgnoreCase));
        }

        foreach (var t in filtered.OrderBy(t => t.ShortDescription))
            FilteredTitles.Add(t);
    }

    private static async Task<TitleViewModel> CreateViewModel(InstalledTitle title, SdTitleScanner scanner, SdCrypto sdCrypto, KeyStore keyStore)
    {
        var vm = new TitleViewModel { Title = title };

        try
        {
            var record0 = title.Contents.OrderBy(c => c.ContentIndex).FirstOrDefault();

            if (record0 is null)
                return vm;

            string filename = record0.ContentIdHex + ".app";
            string normalPath = Path.Combine(title.ContentPath, filename);
            string dlcPath = Path.Combine(title.ContentPath, "00000000", filename);
            string? appFile = File.Exists(normalPath) ? normalPath : File.Exists(dlcPath) ? dlcPath : null;

            if (appFile is null)
                return vm;

            string sdPath = appFile.StartsWith(scanner.Id1Path, StringComparison.OrdinalIgnoreCase) ? appFile[scanner.Id1Path.Length..].Replace('\\', '/') : appFile.Replace('\\', '/');
            using var raw = File.OpenRead(appFile);
            using var sdStream = new SdDecryptStream(raw, sdPath, sdCrypto, leaveOpen: true);
            byte[] ncchBuf = new byte[0x200];

            sdStream.ReadExactly(ncchBuf, 0, 0x200);
            sdStream.Position = 0;

            var ncchHeader = NcchHeader.Parse(ncchBuf, 0);
            Stream ncchStream = ncchHeader.NoCrypto ? sdStream : new NcchDecryptionStream(sdStream, 0, keyStore, leaveOpen: true);
            SmdhInfo? smdhInfo;
            await using (ncchStream)
                smdhInfo = await NcchGameInfoReader.LoadAsync(ncchStream);

            vm.ShortDescription = smdhInfo?.ShortDescription ?? string.Empty;
            vm.Publisher = smdhInfo?.Publisher ?? string.Empty;
            vm.ProductCode = ncchHeader.ProductCodeString;
            vm.Crypto = !ncchHeader.NoCrypto;

            if (smdhInfo?.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, smdhInfo.IconPixels, 48 * 4);
                bitmap.Freeze();
                vm.Icon = bitmap;
            }
        }
        catch { }

        return vm;
    }

    private void SetLoading(bool loading)
    {
        _isLoading = loading;
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ListVisibility));
    }
}