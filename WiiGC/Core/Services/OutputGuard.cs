namespace WiiGC.Core.Services;

internal static class OutputGuard
{
    public static void DeleteIfFailed(string path, bool succeeded)
    {
        if (succeeded)
            return;

        TryDelete(path);
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}