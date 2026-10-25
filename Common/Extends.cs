using System.Diagnostics;
namespace Common;

public static class ProcessExtensions
{
    public static void OpenFolder(this string folder)
    {
        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }
}