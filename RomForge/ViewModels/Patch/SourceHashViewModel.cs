using Common.WPF.ViewModels;
using RomForge.Core.Models.Patch;
using RomForge.Core.Services.Patch;
using System.Collections.ObjectModel;
using System.IO;

namespace RomForge.ViewModels.Patch;

public class SourceHashViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;
    private bool _isCalculating;
    private bool _hasError;
    private bool _isCancelled;
    private int _progressPercent;
    private int _skippedCount;

    public SourceHashViewModel()
    {
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasEntries));
    }

    public Func<Task<GdRomHashLayout>>? RequestGdRomLayoutAsync { get; set; }

    public ObservableCollection<HashEntryItem> Entries { get; } = [];

    public bool HasEntries => Entries.Count > 0;

    public bool IsCalculating
    {
        get => _isCalculating;
        private set => SetProperty(ref _isCalculating, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        private set => SetProperty(ref _isCancelled, value);
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        private set
        {
            if (SetProperty(ref _progressPercent, value))
                OnPropertyChanged(nameof(CalculatingText));
        }
    }

    public string CalculatingText => $"해시 계산 중... {ProgressPercent}%";

    public int SkippedCount
    {
        get => _skippedCount;
        private set
        {
            if (SetProperty(ref _skippedCount, value))
            {
                OnPropertyChanged(nameof(HasSkipped));
                OnPropertyChanged(nameof(SkippedText));
            }
        }
    }

    public bool HasSkipped => SkippedCount > 0;

    public string SkippedText => $"용량이 작은 {SkippedCount}개 파일은 표시하지 않았습니다";

    public async Task StartAsync(string? path)
    {
        _cts?.Cancel();
        _cts = null;

        ClearResult();

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _cts = cts;
        ProgressPercent = 0;
        IsCalculating = true;

        var progress = new Progress<double>(value =>
        {
            if (!ct.IsCancellationRequested)
                ProgressPercent = (int)(value * 100);
        });

        var entryProgress = new Progress<(string Name, RomHashResult Hashes)>(entry =>
        {
            if (!ct.IsCancellationRequested)
                AddEntry(entry.Name, entry.Hashes);
        });

        try
        {
            if (SourceArchiveExtractor.IsArchivePath(path))
            {
                int skipped = await SourceArchiveExtractor.ComputeHashesAsync(path, (name, hashes) => ((IProgress<(string Name, RomHashResult Hashes)>)entryProgress).Report((Path.GetFileName(name), hashes)), progress, ct);

                if (ct.IsCancellationRequested)
                    return;

                SkippedCount = skipped;
            }
            else if (string.Equals(Path.GetExtension(path), ".chd", StringComparison.OrdinalIgnoreCase))
            {
                var gdRomLayout = GdRomHashLayout.Gdi;

                if (RequestGdRomLayoutAsync is not null && await ChdHasher.NeedsGdRomLayoutChoiceAsync(path, ct))
                {
                    gdRomLayout = await RequestGdRomLayoutAsync();

                    if (ct.IsCancellationRequested)
                        return;
                }

                var (Name, Hashes) = await ChdHasher.HashAsync(path, gdRomLayout, progress, ct);

                if (ct.IsCancellationRequested)
                    return;

                AddEntry(Name, Hashes);
            }
            else
            {
                var hashes = await RomHasher.HashFileAsync(path, progress, ct);

                if (ct.IsCancellationRequested)
                    return;

                AddEntry(Path.GetFileName(path), hashes);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            HasError = true;
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
                IsCalculating = false;
        }
    }

    public void Cancel()
    {
        if (!IsCalculating)
            return;

        _cts?.Cancel();

        _cts = null;
        IsCalculating = false;
        IsCancelled = true;
    }

    private void AddEntry(string name, RomHashResult hashes) => Entries.Add(new HashEntryItem(name, hashes.Crc32, hashes.Md5, hashes.Sha1));

    private void ClearResult()
    {
        IsCancelled = false;
        IsCalculating = false;
        HasError = false;
        SkippedCount = 0;

        Entries.Clear();
    }
}