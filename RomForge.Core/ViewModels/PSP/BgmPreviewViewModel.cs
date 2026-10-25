using Common;
using NAudio.Wave;
using RomForge.Core.UI.Command;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace RomForge.Core.ViewModels.PSP;

public class BgmPreviewViewModel : ViewModelBase
{
    private readonly Action<string, LogLevel> _log;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    private byte[]? _wav;
    private WaveOutEvent? _waveOut;
    private WaveStream? _waveStream;
    private bool _isSeeking;
    private bool _isPlaying;
    private double _positionSeconds;
    private double _durationSeconds;

    public BgmPreviewViewModel(Action<string, LogLevel> log)
    {
        _log = log;

        TogglePlayCommand = new RelayCommand(_ => TogglePlay());
        _timer.Tick += (_, _) => UpdatePosition();
    }

    public ICommand TogglePlayCommand { get; }

    public bool HasPreview => _wav is { Length: > 0 };

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        private set
        {
            if (SetProperty(ref _durationSeconds, value))
                OnPropertyChanged(nameof(DurationText));
        }
    }

    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            value = Math.Clamp(value, 0, _durationSeconds);

            if (Math.Abs(value - _positionSeconds) < 0.001)
                return;

            SetPosition(value);

            if (!_isSeeking)
                Seek(value);
        }
    }

    public string PositionText => FormatTime(_positionSeconds);

    public string DurationText => FormatTime(_durationSeconds);

    public void Load(byte[]? wav)
    {
        Stop();

        _wav = wav;

        DurationSeconds = GetDuration(wav);

        OnPropertyChanged(nameof(HasPreview));
        Play();
    }

    public void Clear()
    {
        Stop();

        DurationSeconds = 0;
        _wav = null;

        OnPropertyChanged(nameof(HasPreview));
    }

    public void Stop()
    {
        Release();
        SetPosition(0);
    }

    public void BeginSeek() => _isSeeking = true;

    public void EndSeek()
    {
        _isSeeking = false;

        Seek(_positionSeconds);
    }

    private void TogglePlay()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    private bool EnsurePlayer()
    {
        if (_waveOut != null && _waveStream != null)
            return true;

        if (_wav == null || _wav.Length == 0)
            return false;

        try
        {
            _waveStream = new WaveFileReader(new MemoryStream(_wav));
            _waveOut = new WaveOutEvent();

            _waveOut.Init(_waveStream);

            _waveOut.PlaybackStopped += OnPlaybackStopped;

            return true;
        }
        catch (Exception ex)
        {
            _log($"미리듣기 재생 중 오류: {ex.Message}", LogLevel.Error);
            Release();
            return false;
        }
    }

    private void Play()
    {
        if (!EnsurePlayer())
            return;

        try
        {
            _waveOut!.Play();

            IsPlaying = true;

            _timer.Start();
        }
        catch (Exception ex)
        {
            _log($"미리듣기 재생 중 오류: {ex.Message}", LogLevel.Error);
            Release();
        }
    }

    private void Pause()
    {
        _waveOut?.Pause();
        _timer.Stop();

        IsPlaying = false;

        UpdatePosition();
    }

    private void Seek(double seconds)
    {
        if (!EnsurePlayer())
            return;

        _waveStream!.CurrentTime = TimeSpan.FromSeconds(seconds);
    }

    private void UpdatePosition()
    {
        if (_isSeeking || _waveStream == null)
            return;

        SetPosition(_waveStream.CurrentTime.TotalSeconds);
    }

    private void SetPosition(double seconds)
    {
        _positionSeconds = Math.Clamp(seconds, 0, _durationSeconds);

        OnPropertyChanged(nameof(PositionSeconds));
        OnPropertyChanged(nameof(PositionText));
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!ReferenceEquals(sender, _waveOut))
            return;

        if (e.Exception != null)
            _log($"미리듣기 재생 중 오류: {e.Exception.Message}", LogLevel.Error);

        Release();
        SetPosition(0);
    }

    private void Release()
    {
        _timer.Stop();

        try
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;

                _waveOut.Stop();
                _waveOut.Dispose();

                _waveOut = null;
            }

            _waveStream?.Dispose();

            _waveStream = null;
        }
        catch { }
        finally
        {
            IsPlaying = false;
        }
    }

    private static double GetDuration(byte[]? wav)
    {
        if (wav == null || wav.Length == 0)
            return 0;

        using var reader = new WaveFileReader(new MemoryStream(wav));

        return reader.TotalTime.TotalSeconds;
    }

    private static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}