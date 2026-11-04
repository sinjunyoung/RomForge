using Common.Models;
using System.Diagnostics;
namespace Common.Services;

public static class Extensions
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

    public static void ReportStatus(this IProgress<ProgressInfo>? progress, string label, int percent = -1) => progress?.Report(new ProgressInfo(percent, label, string.Empty, string.Empty, string.Empty));
}