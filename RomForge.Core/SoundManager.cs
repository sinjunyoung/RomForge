using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace RomForge.Core;

public static class SoundManager
{
    private static readonly object _lock = new();
    private static WaveOutEvent? _waveOutDevice;
    private static WaveStream? _audioFileReader;
    private static Stream? _audioStream;

    public static async Task PlayFromWpfResourceAsync(string packUriString)
    {
        Stream stream;
        string extension = Path.GetExtension(packUriString).ToLowerInvariant();

        try
        {
            if (Path.IsPathRooted(packUriString) || File.Exists(packUriString))
                stream = new FileStream(packUriString, FileMode.Open, FileAccess.Read, FileShare.Read);
            else
            {
                var uri = new Uri(packUriString, UriKind.Absolute);
                StreamResourceInfo resourceStreamInfo = Application.GetResourceStream(uri) ?? throw new FileNotFoundException($"WPF 리소스를 찾을 수 없습니다: {packUriString}");
                var memoryStream = new MemoryStream();

                resourceStreamInfo.Stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                stream = memoryStream;

                extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"오디오 스트림 로드 오류: {ex.Message}");
            return;
        }

        lock (_lock)
            StopInternal();

        await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    _audioStream = stream;

                    if (extension == ".wav")
                        _audioFileReader = new WaveFileReader(_audioStream);
                    else if (extension == ".mp3")
                        _audioFileReader = new Mp3FileReader(_audioStream);
                    else
                        throw new NotSupportedException($"지원하지 않는 오디오 형식입니다: {extension}");

                    _waveOutDevice = new WaveOutEvent();

                    _waveOutDevice.Init(_audioFileReader);

                    _waveOutDevice.PlaybackStopped += (s, e) => {
                        lock (_lock) 
                            DisposeResources();
                    };

                    _waveOutDevice.Play();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"오디오 재생 중 오류 발생: {ex.Message}");
                    DisposeResources();
                }
            }
        });
    }

    public static async Task PlayFromFileAsync(string filePath) => await PlayFromWpfResourceAsync(filePath);

    public static void Stop()
    {
        lock (_lock)
            StopInternal();
    }

    private static void StopInternal()
    {
        try
        {
            _waveOutDevice?.Stop();
        }
        finally
        {
            DisposeResources();
        }
    }

    private static void DisposeResources()
    {
        _waveOutDevice?.Dispose();
        _waveOutDevice = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;

        _audioStream?.Dispose();
        _audioStream = null;
    }
}