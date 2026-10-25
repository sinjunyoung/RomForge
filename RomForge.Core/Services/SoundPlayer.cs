using System.IO;

namespace RomForge.Core.Services;

public static class SoundPlayer
{
    public static async Task PlayCompleteAsync() => await PlayAsync(AppConfig.Instance.Common.CompleteSoundPath, "complete.mp3");

    public static async Task PlayCancelAsync() => await PlayAsync(AppConfig.Instance.Common.CancelSoundPath, "cancel.mp3");

    public static async Task PlayErrorAsync() => await PlayAsync(AppConfig.Instance.Common.ErrorSoundPath, "error.mp3");

    public static async Task PlayAsync(string targetPath, string defaultFileName)
    {
        if (!AppConfig.Instance.Common.IsMuted)
        {
            string path = string.IsNullOrWhiteSpace(targetPath) ? defaultFileName : targetPath;

            if (File.Exists(path))
                await SoundManager.PlayFromFileAsync(path);
            else
            {
                string packUriString = $"pack://application:,,,/Assets/Sounds/{defaultFileName}";
                await SoundManager.PlayFromWpfResourceAsync(packUriString);
            }
        }
    }
}