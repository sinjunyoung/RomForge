using Common.WPF.ViewModels;
using RomForge.Core.Services.Patch;
using System.IO;

namespace RomForge.ViewModels.Patch;

public class SourceHashViewModel : ViewModelBase
{
    private CancellationTokenSource? _cts;
    private bool _isCalculating;
    private bool _hasResult;
    private bool _hasError;
    private bool _isCancelled;
    private int _progressPercent;
    private string? _targetName;
    private string _crc32 = string.Empty;
    private string _md5 = string.Empty;
    private string _sha1 = string.Empty;

    public bool IsCalculating
    {
        get => _isCalculating;
        private set => SetProperty(ref _isCalculating, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        private set => SetProperty(ref _hasResult, value);
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

    public string? TargetName
    {
        get => _targetName;
        private set
        {
            if (SetProperty(ref _targetName, value))
                OnPropertyChanged(nameof(HasTargetName));
        }
    }

    public bool HasTargetName => !string.IsNullOrEmpty(TargetName);

    public string Crc32
    {
        get => _crc32;
        private set => SetProperty(ref _crc32, value);
    }

    public string Md5
    {
        get => _md5;
        private set => SetProperty(ref _md5, value);
    }

    public string Sha1
    {
        get => _sha1;
        private set => SetProperty(ref _sha1, value);
    }

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

        try
        {
            string? targetName = null;
            RomHashResult hashes;

            if (SourceArchiveExtractor.IsArchivePath(path))
            {
                var (EntryName, Hashes) = await SourceArchiveExtractor.ComputeHashAsync(path, progress, ct);

                targetName = Path.GetFileName(EntryName);
                hashes = Hashes;
            }
            else
            {
                hashes = await RomHasher.HashFileAsync(path, progress, ct);
            }

            if (ct.IsCancellationRequested)
                return;

            TargetName = targetName;
            Crc32 = hashes.Crc32;
            Md5 = hashes.Md5;
            Sha1 = hashes.Sha1;
            HasResult = true;
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

    private void ClearResult()
    {
        IsCancelled = false;
        IsCalculating = false;
        HasResult = false;
        HasError = false;
        TargetName = null;
        Crc32 = string.Empty;
        Md5 = string.Empty;
        Sha1 = string.Empty;
    }
}