using PSP.PBP.Models;
using PSP.PBP.Services;
using RomForge.Core;
using RomForge.Core.Services.PSP;
using RomForge.Core.ViewModels;
using System.Windows.Media.Imaging;

namespace RomForge.ViewModels.PSP;

public class PackingImagesViewModel : ViewModelBase
{
    private readonly CoverArtUpdater _coverArtUpdater = new();

    private string? _lastGameId;
    private bool _isDownloading;
    private BitmapImage? _icon0Image;
    private BitmapImage? _pic0Image;
    private BitmapImage? _pic1Image;
    private BitmapImage? _bootLogoImage;

    public PackingImagesViewModel()
    {
        Icon0Image = Icon0Bytes.ToBitmapImage();
        Pic0Image = Pic0Bytes.ToBitmapImage();
        Pic1Image = Pic1Bytes.ToBitmapImage();
    }

    public byte[] Icon0Bytes { get; private set; } = EmbeddedAssetProvider.GetDefaultIcon0();

    public byte[] Pic0Bytes { get; private set; } = EmbeddedAssetProvider.GetDefaultPic0();

    public byte[] Pic1Bytes { get; private set; } = EmbeddedAssetProvider.GetDefaultPic1();

    public byte[]? BootLogoBytes { get; private set; }

    public BitmapImage? Icon0Image
    {
        get => _icon0Image;
        private set => SetProperty(ref _icon0Image, value);
    }

    public BitmapImage? Pic0Image
    {
        get => _pic0Image;
        private set => SetProperty(ref _pic0Image, value);
    }

    public BitmapImage? Pic1Image
    {
        get => _pic1Image;
        private set => SetProperty(ref _pic1Image, value);
    }

    public BitmapImage? BootLogoImage
    {
        get => _bootLogoImage;
        private set
        {
            SetProperty(ref _bootLogoImage, value);
            OnPropertyChanged(nameof(HasBootLogo));
            OnPropertyChanged(nameof(ShowBootLogoHint));
        }
    }

    public bool HasBootLogo => BootLogoImage != null;

    public bool ShowBootLogoHint => !HasBootLogo;

    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
    }

    public void SetIcon0FromBytes(byte[] rawBytes)
    {
        var (bytes, img) = AssetImageEditor.Resize(rawBytes, 80, 80);

        Icon0Bytes = bytes;
        Icon0Image = img;
    }

    public void SetPic0FromBytes(byte[] rawBytes)
    {
        var (bytes, img) = AssetImageEditor.Resize(rawBytes, 270, 150);

        Pic0Bytes = bytes;
        Pic0Image = img;
    }

    public void SetPic1FromBytes(byte[] rawBytes)
    {
        var (bytes, img) = AssetImageEditor.Resize(rawBytes, 480, 272);

        Pic1Bytes = bytes;
        Pic1Image = img;
    }

    public void SetBootLogoFromBytes(byte[] rawBytes)
    {
        var (bytes, img) = AssetImageEditor.Resize(rawBytes, 480, 272);

        BootLogoBytes = bytes;
        BootLogoImage = img;
    }

    public void ResetBaseImages()
    {
        Icon0Bytes = EmbeddedAssetProvider.GetDefaultIcon0();
        Icon0Image = Icon0Bytes.ToBitmapImage();
        Pic0Bytes = EmbeddedAssetProvider.GetDefaultPic0();
        Pic0Image = Pic0Bytes.ToBitmapImage();
        Pic1Bytes = EmbeddedAssetProvider.GetDefaultPic1();
        Pic1Image = Pic1Bytes.ToBitmapImage();
    }

    public void ResetBootLogo()
    {
        BootLogoBytes = null;
        BootLogoImage = null;
    }

    public void ResetAll()
    {
        _lastGameId = null;

        ResetBaseImages();
        ResetBootLogo();
    }

    public async Task<string?> UpdateFromGameIdAsync(string? gameId)
    {
        if (gameId == null || gameId == _lastGameId || gameId is "인식중..." or "인식실패")
            return null;

        _lastGameId = gameId;
        IsDownloading = true;

        try
        {
            var result = await _coverArtUpdater.FetchAsync(gameId);

            if (result == null)
                return null;

            Icon0Bytes = result.Icon0Png;
            Icon0Image = Icon0Bytes.ToBitmapImage();

            Pic0Bytes = result.Pic0Png;
            Pic0Image = Pic0Bytes.ToBitmapImage();

            Pic1Bytes = result.Pic1Png;
            Pic1Image = Pic1Bytes.ToBitmapImage();

            return result.ETitle;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public PbpAssets BuildAssets(byte[]? bgm) => new()
    {
        Icon0Png = Icon0Bytes.ResizePng(80, 80),
        Pic0Png = Pic0Bytes.ResizePng(480, 272),
        Pic1Png = Pic1Bytes.ResizePng(480, 272),
        BootPng = BootLogoBytes?.ResizePng(480, 272),
        Snd0At3 = bgm,
        DataPsp = EmbeddedAssetProvider.GetDefaultData()
    };
}