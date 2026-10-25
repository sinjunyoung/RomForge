using LibHac.Ns;
using NSW.Core;
using NSW.Core.Converters;
using NSW.Core.Models;
using NSW.Core.Services;
using NSW.Core.ViewModels;
using NSW.HacPack.Models;
using RomForge.Core;
using SixLabors.ImageSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RomForge.Controls.Switch;

public partial class LanguageTabControl : UserControl
{
    public ObservableCollection<LanguageItem> LanguageList { get; } = [];

    public GameMetadata? CurrentMetadata { get; private set; }

    public ApplicationControlProperty.Language ForcedLanguage { get; private set; } = ApplicationControlProperty.Language.None;

    public byte? TargetIdOffset
    {
        get
        {
            if (cbxMultiRom.Dispatcher.CheckAccess())
                return cbxMultiRom.SelectedItem is byte offset ? offset : null;
            else
                return cbxMultiRom.Dispatcher.Invoke(new Func<byte?>(() => TargetIdOffset));
        }
    }

    public ulong? OverrideTitleId
    {
        get
        {
            if (!txtOverrideTitleId.Dispatcher.CheckAccess())
                return txtOverrideTitleId.Dispatcher.Invoke(new Func<ulong?>(() => OverrideTitleId));

            string text = txtOverrideTitleId.Text.Trim();

            if (string.IsNullOrEmpty(text))
                return null;

            return ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong titleId) ? titleId : null;
        }
    }

    private readonly string[] SupportedExts = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"];

    public LanguageTabControl()
    {
        Resources.Add("LanguageToFlagConverter", new LanguageToFlagConverter());
        Resources.Add("LanguageToFlagImageConverter", new LanguageToFlagImageConverter());
        InitializeComponent();

        lbLanguages.ItemsSource = LanguageList;
        lbLanguages.SelectionChanged += LbLanguages_SelectionChanged;
        txtGameName.TextChanged += TxtGameName_TextChanged;
        txtPublisher.TextChanged += TxtPublisher_TextChanged;
        txtOverrideTitleId.PreviewTextInput += TxtOverrideTitleId_PreviewTextInput;

        DataObject.AddPastingHandler(txtOverrideTitleId, TxtOverrideTitleId_Pasting);
    }

    private static void TxtOverrideTitleId_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !IsHexString(e.Text);

    private void TxtOverrideTitleId_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text) || !IsHexString((string)e.DataObject.GetData(DataFormats.Text)))
            e.CancelCommand();
    }

    private static bool IsHexString(string s) => s.Length > 0 && s.All(Uri.IsHexDigit);

    public async Task UpdateLanguageTabAsync(IList<GameFile> gameFiles, string? unpackedDir = null)
    {
        ResetUI();

        GameMetadata? metadata = await Task.Run(() => LoadGameMetadata(gameFiles, unpackedDir));

        if (metadata != null)
        {
            CurrentMetadata = metadata;
            BindMetadataToUI(metadata);
        }

        txtOverrideTitleId.Text = (gameFiles.FirstOrDefault(f => f.FileType.Contains('B')) ?? gameFiles.FirstOrDefault(f => !string.IsNullOrEmpty(f.TitleID)))?.TitleID ?? string.Empty;
    }

    public void SyncMetadataFromUI()
    {
        if (CurrentMetadata == null)
            return;

        foreach (var item in LanguageList)
        {
            var target = CurrentMetadata.Languages
                .FirstOrDefault(l => l.Language == item.Language);

            if (target != null) 
                target.Flag = item.IsSelected;
        }
    }

    private void ResetUI()
    {
        LanguageList.Clear();

        txtGameName.Text = string.Empty;
        txtPublisher.Text = string.Empty;
        txtOverrideTitleId.Text = string.Empty;
        imgGame.Source = null;
    }

    private static GameMetadata? LoadGameMetadata(IList<GameFile> gameFiles, string? unpackedDir = null)
    {
        var keySet = KeySetProvider.Instance.KeySet;

        if (keySet == null)
            return null;

        var validPaths = gameFiles
            .Where(f => !f.IsKeyMissing)
            .Select(f => f.FilePath)
            .ToList();

        if (validPaths.Count > 0)
            return MetadataService.GetGameMetadata(keySet, validPaths);

        if (unpackedDir != null)
            return MetadataService.GetGameMetadataFromUnpacked(unpackedDir);

        return null;
    }

    private void BindMetadataToUI(GameMetadata metadata)
    {
        var items = metadata.Languages.Select(info => new LanguageItem
        {
            Language = info.Language,
            TitleName = info.TitleName,
            Publisher = info.Publisher,
            Indices = metadata.Indices,
            Logo = info.LogoData,
            IsSelected = info.Flag
        }).ToList();

        LanguageList.Clear();

        foreach (var item in items)
            LanguageList.Add(item);

        if (LanguageList.Count > 0)
            lbLanguages.SelectedIndex = 0;
    }

    private void LbLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lbLanguages.SelectedItem is not LanguageItem selectedItem)
            return;

        txtGameName.Text = selectedItem.TitleName;
        txtPublisher.Text = selectedItem.Publisher;
        cbxMultiRom.ItemsSource = selectedItem.Indices;
        imgGame.Source = selectedItem.Logo is { Length: > 0 } ? selectedItem.Logo.ToBitmapImage() : null;
    }

    private void TxtGameName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (lbLanguages.SelectedItem is not LanguageItem selectedItem || CurrentMetadata == null)
            return;

        selectedItem.TitleName = txtGameName.Text;

        var target = CurrentMetadata.Languages
            .FirstOrDefault(l => l.Language == selectedItem.Language);

        if (target != null) target.TitleName = txtGameName.Text;
    }

    private void TxtPublisher_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (lbLanguages.SelectedItem is not LanguageItem selectedItem || CurrentMetadata == null)
            return;

        selectedItem.Publisher = txtPublisher.Text;

        var target = CurrentMetadata.Languages
            .FirstOrDefault(l => l.Language == selectedItem.Language);

        if (target != null) 
            target.Publisher = txtPublisher.Text;
    }

    private void BtnForceLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (lbLanguages.SelectedItem is not LanguageItem selectedItem)
            return;

        ForcedLanguage = selectedItem.Language;

        MessageBoxHelper.ShowInfo($"강제 언어가 {selectedItem.Language}로 설정되었습니다.");
    }

    private void BtnForceLanguageCancel_Click(object sender, RoutedEventArgs e)
    {
        ForcedLanguage = ApplicationControlProperty.Language.None;

        MessageBoxHelper.ShowInfo("강제 언어 설정이 취소되었습니다.");
    }

    private void ImgGame_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

            if (files?.Length > 0 && SupportedExts.Contains(Path.GetExtension(files[0]).ToLowerInvariant()))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;

                return;
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void ImgGame_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);

        if (files is not { Length: > 0 })
            return;

        string ext = Path.GetExtension(files[0]).ToLowerInvariant();

        if (!SupportedExts.Contains(ext))
            return;

        try
        {
            byte[] finalBytes = files[0].ToImageBytes();

            imgGame.Source = finalBytes.ToBitmapImage();

            if (lbLanguages.SelectedItem is LanguageItem selectedItem && CurrentMetadata != null)
            {
                selectedItem.Logo = finalBytes;

                var target = CurrentMetadata.Languages.FirstOrDefault(l => l.Language == selectedItem.Language);

                if (target != null)
                    target.LogoData = finalBytes;
            }
        }
        catch (Exception ex)
        {
            MessageBoxHelper.ShowError($"이미지 로드 실패: {ex.Message}");
        }
    }

    private void ImgGame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (imgGame.Source is not BitmapSource bitmapSource)
            return;

        var selectedLanguage = lbLanguages.SelectedItem as LanguageItem;
        string fileName = $"{selectedLanguage?.TitleName}_{selectedLanguage?.Language}.png";

        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                var encoder = new PngBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(fs);
            }

            var data = new DataObject();

            data.SetFileDropList([tempFilePath]);
            DragDrop.DoDragDrop(imgGame, data, DragDropEffects.Copy);
        }
        finally
        {
            if (File.Exists(tempFilePath))
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { }
        }
    }
}