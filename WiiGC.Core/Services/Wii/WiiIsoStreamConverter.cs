namespace WiiGC.Core.Services.Wii;

public static class WiiIsoStreamConverter
{
    public static void RebuildToWbfs(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        using var source = WiiRebuiltIsoSource.Create(input, replacements, prepareProgress, ct);

        IsoToWbfsConverter.Convert(source, outputPath, convertProgress, ct);
    }

    public static void RebuildToRvz(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, int compressionLevel = 5, int chunkSize = 131072, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        using var source = WiiRebuiltIsoSource.Create(input, replacements, prepareProgress, ct);

        IsoToRvzConverter.Convert(source, outputPath, compressionLevel, chunkSize, convertProgress, ct);
    }
}