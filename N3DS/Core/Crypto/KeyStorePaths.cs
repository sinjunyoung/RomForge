namespace N3DS.Core.Crypto;

public static class KeyStorePaths
{
    public static string? CustomAesKeys { get; set; }

    public static string? CustomBoot9 { get; set; }

    public static string? CustomSeedDb { get; set; }

    public static string AesKeys => Resolve(CustomAesKeys, "aes_keys.txt");

    public static string Boot9 => Resolve(CustomBoot9, "boot9.bin");

    public static string SeedDb => Resolve(CustomSeedDb, "seeddb.bin");

    public static bool HasUsableKeys => File.Exists(AesKeys) && File.Exists(Boot9) && File.Exists(SeedDb);

    private static string Resolve(string? customPath, string defaultFileName)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        return Path.Combine(AppContext.BaseDirectory, defaultFileName);
    }
}