using Common;
using RomForge.Core.Models.PSP;
using RomForge.Core.Services;
using RomForge.Core.Services.PSP;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace RomForge.Core.ViewModels.PSP;

public class DiscListViewModel(Action<string, LogLevel> log) : ViewModelBase
{
    public const int MaxItems = 5;

    private readonly DiscImportService _importService = new();
    private bool _isValidating;

    public ObservableCollection<DiscFileItem> FileItems { get; } = [];

    public Visibility HintVisibility => FileItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool HasPresetConfig => Primary?.HasPresetConfig ?? false;

    public DiscFileItem? Primary => FileItems.FirstOrDefault(i => i.No == 1);

    public bool IsValidating
    {
        get => _isValidating;
        set => SetProperty(ref _isValidating, value);
    }

    public event EventHandler? Changed;

    public event EventHandler? Cleared;

    public static string GetFileDialogFilter() => DiscImportService.GetFileDialogFilter();

    public async void AddPaths(IEnumerable<string> paths)
    {
        try
        {
            await AddPathsAsync(paths);
        }
        catch (Exception ex)
        {
            log($"파일 추가 중 오류: {ex.Message}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
            IsValidating = false;
        }
    }

    public void Remove(IEnumerable<DiscFileItem> items)
    {
        foreach (var item in items.ToList())
            FileItems.Remove(item);

        OnPropertyChanged(nameof(HintVisibility));
        DiscListSorter.SortAndRenumber(FileItems);
        OnPropertyChanged(nameof(HasPresetConfig));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        FileItems.Clear();
        OnPropertyChanged(nameof(HintVisibility));
        OnPropertyChanged(nameof(HasPresetConfig));
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var inputPaths = paths.ToList();

        if (inputPaths.Count == 0)
            return;

        var existingPaths = FileItems.Select(f => f.FilePath);
        var room = MaxItems - FileItems.Count;

        IsValidating = true;

        ImportResult result;

        try
        {
            result = await _importService.ImportAsync(inputPaths, existingPaths, room);
        }
        finally
        {
            IsValidating = false;
        }

        foreach (var failure in result.Failures)
        {
            log($"[{failure.FileName}] GameID 인식 실패: {failure.Reason}", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }

        foreach (var disc in result.Imported)
        {
            var item = new DiscFileItem(disc.Path)
            {
                GameId = disc.GameId,
                FileSizeBytes = disc.Size,
                PresetConfigBytes = disc.PresetConfig
            };

            FileItems.Add(item);
        }

        if (result.OverLimitSkipped > 0)
        {
            log($"최대 {MaxItems}개까지만 등록할 수 있습니다. {result.OverLimitSkipped}개 파일이 제외되었습니다.", LogLevel.Error);
            await SoundPlayer.PlayErrorAsync();
        }

        if (result.Failures.Count > 0)
            log($"{result.Failures.Count}개 파일에서 GameID 인식에 실패해 제외되었습니다.", LogLevel.Error);

        if (result.Imported.Count > 0)
        {
            OnPropertyChanged(nameof(HintVisibility));
            DiscListSorter.SortAndRenumber(FileItems);
            OnPropertyChanged(nameof(HasPresetConfig));
            Changed?.Invoke(this, EventArgs.Empty);
        }

        CommandManager.InvalidateRequerySuggested();
    }
}