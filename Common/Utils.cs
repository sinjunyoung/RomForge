using System.Buffers;
using System.Diagnostics;

namespace Common;

public static class Utils
{
    public static (int pct, string label, double currentMiB, double totalMiB) CalculateProgress(long readBytes, long totalBytes, string label)
    {
        double currentMiB = (double)readBytes / 1024 / 1024;
        double totalMiB = (double)totalBytes / 1024 / 1024;
        int pct = totalBytes > 0 ? Math.Min(100, (int)((double)readBytes / totalBytes * 100)) : 0;
        string formattedLabel = $"{label} ({currentMiB:N0}MiB / {totalMiB:N2}MiB)";

        return (pct, formattedLabel, currentMiB, totalMiB);
    }

    public static string ToAppVersionString()
    {
        string processPath = Environment.ProcessPath ?? string.Empty;
        var info = FileVersionInfo.GetVersionInfo(processPath);
        DateTime buildDate = File.GetLastWriteTime(processPath);

        return $"{info.ProductMajorPart}.{info.ProductMinorPart}.{info.ProductPrivatePart} (Build: {buildDate:yyyy'/'MM'/'dd})";
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes >= 1_073_741_824) 
            return $"{bytes / 1_073_741_824.0:F1} GB";

        if (bytes >= 1_048_576) 
            return $"{bytes / 1_048_576.0:F1} MB";

        return $"{bytes / 1024.0:F1} KB";
    }

    public static string GetUniqueFilePath(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);

        while (File.Exists(filePath))
        {
            fileName += "_";
            filePath = Path.Combine(directory, fileName + extension);
        }

        return filePath;
    }

    public static string GetUniqueFolderPath(string folderPath)
    {
        while (Directory.Exists(folderPath))
            folderPath += "_";

        return folderPath;
    }

    public static async Task CopyStreamAsync(Stream src, Stream dst, Action<long>? onRead = null, CancellationToken ct = default)
    {
        const int bufferSize = 81920;
        byte[] buf = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            int read;
            while ((read = await src.ReadAsync(buf.AsMemory(0, bufferSize), ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                onRead?.Invoke(read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public static async Task CopyStreamAsync(Stream src, Stream dst, long length, Action<long>? onRead = null, CancellationToken ct = default)
    {
        const int bufferSize = 81920;
        byte[] buf = ArrayPool<byte>.Shared.Rent(bufferSize);
        long remaining = length;

        try
        {
            int read;
            while (remaining > 0 && (read = await src.ReadAsync(buf.AsMemory(0, (int)Math.Min(bufferSize, remaining)), ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                onRead?.Invoke(read);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }
}