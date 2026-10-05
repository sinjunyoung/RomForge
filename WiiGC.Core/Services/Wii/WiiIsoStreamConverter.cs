namespace WiiGC.Core.Services.Wii;

public static class WiiIsoStreamConverter
{
    public static void RepackFolderToIso(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, Action<double>? prepareProgress = null, Action<double>? writeProgress = null, CancellationToken ct = default)
    {
        using var source = WiiRebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, ct);

        bool succeeded = false;

        try
        {
            using (var output = SparseFile.Create(outputPath, source.Length))
            {
                byte[] buffer = new byte[0x100000];
                long length = source.Length;
                int lastPermille = -1;

                for (long offset = 0; offset < length; offset += buffer.Length)
                {
                    ct.ThrowIfCancellationRequested();

                    int size = (int)Math.Min(buffer.Length, length - offset);
                    var span = buffer.AsSpan(0, size);

                    source.Read(offset, span);

                    if (span.ContainsAnyExcept((byte)0))
                        RandomAccess.Write(output, span, offset);

                    int permille = (int)((offset + size) * 1000 / length);

                    if (permille != lastPermille)
                    {
                        lastPermille = permille;

                        writeProgress?.Invoke(permille / 1000.0);
                    }
                }
            }

            succeeded = true;
        }
        finally
        {
            if (!succeeded && File.Exists(outputPath))
                try { File.Delete(outputPath); } catch { }
        }
    }

    public static void RepackFolderToWbfs(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, CancellationToken ct = default)
    {
        using var source = WiiRebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, ct);

        IsoToWbfsConverter.Convert(source, outputPath, convertProgress, ct);
    }

    public static void RepackFolderToRvz(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, int compressionLevel = 5, int chunkSize = 131072, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, CancellationToken ct = default)
    {
        using var source = WiiRebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, ct);

        IsoToRvzConverter.Convert(source, outputPath, compressionLevel, chunkSize, convertProgress, ct);
    }

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