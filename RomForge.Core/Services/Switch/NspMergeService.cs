using Common;
using LibHac.Common.Keys;
using NSW.Core;

namespace RomForge.Core.Services.Switch;

public static class NspMergeService
{
    public static Task<List<string>> Merge(IReadOnlyList<string> inputPaths, string outputDir, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => RunMergeAll(inputPaths, outputDir, compressionLevel > 0, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, KeySetProvider.Instance.KeySet.Clone(), progress, log, ct);

    public static Task<List<string>> RunMergeAll(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => ContainerMerger.MergeAllAsync(OutputContainer.AsNsp, inputPaths, outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet, progress, log, ct);

    public static Task<List<string>> RunMergeSingle(IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode, bool isValidationEnabled, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => ContainerMerger.MergeSingleAsync(OutputContainer.AsNsp, inputPaths, outputDir, useCompression, compressionLevel, useBlockMode, isValidationEnabled, forceKeyGen0, keySet, progress, log, ct);

    public static Task<string> RunMergeProcess(Models.Switch.BuildRequest req, KeySet keySet, List<NSW.Core.Models.MetadataResult> cachedMeta, bool isValidationEnabled, bool forceKeyGen0, int index, int groupCount, bool hasBase, bool hasUpdate, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
        => ContainerMerger.MergeProcessAsync(OutputContainer.AsNsp, req, keySet, cachedMeta, isValidationEnabled, forceKeyGen0, index, groupCount, hasBase, hasUpdate, progress, log, ct);
}