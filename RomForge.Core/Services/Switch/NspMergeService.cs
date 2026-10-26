using Common;
using LibHac.Common.Keys;

namespace RomForge.Core.Services.Switch;

public static class NspMergeService
{
    public static Task<List<string>> RunMergeAll(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet,
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => ContainerMerger.MergeAllAsync(OutputContainer.AsNsp, inputPaths, outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet, progress, log, ct);

    public static Task<List<string>> RunMergeSingle(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet,
        IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => ContainerMerger.MergeSingleAsync(OutputContainer.AsNsp, inputPaths, outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet, progress, log, ct);
}