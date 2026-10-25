using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

public static class IsoStreamConverter
{
    public static PatchResult RepackFolderToIso(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, Action<double>? prepareProgress = null, Action<double>? writeProgress = null, Action<PatchEntry>? entryLog = null, CancellationToken ct = default)
    {
        using var source = RebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, entryLog, ct);

        bool succeeded = false;

        try
        {
            using (var output = SparseFile.Create(outputPath, source.Length))
            {
                var reporter = new ProgressReporter(source.Length, writeProgress);

                SparseFile.CopyNonZero(source, new FileIsoSink(output), new byte[0x100000], 0, source.Length, reporter, ct);
            }

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }

        return source.Result;
    }

    public static PatchResult RepackFolderToWbfs(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, Action<PatchEntry>? entryLog = null, CancellationToken ct = default)
    {
        using var source = RebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, entryLog, ct);

        IsoToWbfsConverter.Convert(source, outputPath, convertProgress, ct);

        return source.Result;
    }

    public static PatchResult RepackFolderToRvz(string folder, string outputPath, IReadOnlyDictionary<string, string>? overlay, int compressionLevel = 5, int chunkSize = 131072, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, Action<PatchEntry>? entryLog = null, 
        CancellationToken ct = default)
    {
        using var source = RebuiltIsoSource.CreateFromFolder(folder, overlay, prepareProgress, entryLog, ct);

        IsoToRvzConverter.Convert(source, outputPath, compressionLevel, chunkSize, convertProgress, ct);

        return source.Result;
    }

    public static PatchResult RebuildToWbfs(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, Action<PatchEntry>? entryLog = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        using var source = RebuiltIsoSource.Create(input, replacements, prepareProgress, entryLog, ct);

        IsoToWbfsConverter.Convert(source, outputPath, convertProgress, ct);

        return source.Result;
    }

    public static PatchResult RebuildToRvz(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, int compressionLevel = 5, int chunkSize = 131072, Action<double>? prepareProgress = null, Action<double>? convertProgress = null, Action<PatchEntry>? entryLog = null, 
        CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        using var source = RebuiltIsoSource.Create(input, replacements, prepareProgress, entryLog, ct);

        IsoToRvzConverter.Convert(source, outputPath, compressionLevel, chunkSize, convertProgress, ct);

        return source.Result;
    }
}