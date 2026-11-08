namespace WiiU.Core.Services;

public static class WiiUKeyLocator
{
    public static string? CustomPath { get; set; }

    public static string KeysPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomPath) && File.Exists(CustomPath))
                return CustomPath;

            return Path.Combine(AppContext.BaseDirectory, "keys.txt");
        }
    }

    public static bool Exists => File.Exists(KeysPath);
}