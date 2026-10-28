using Common;
using System.IO.Compression;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PatchOutputBuilder
{
    public static Task<MergeResult> BuildMergedFromEntriesAsync(List<BatchSourceEntry> entries, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
        => PatchShared.RunWithItemsAsync(entries, log, items => BuildCoreAsync(items, defaultPatchPath, outputZipPath, target, log, progress, ct));

    private static async Task<MergeResult> BuildCoreAsync(List<SourceItem> items, string defaultPatchPath, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var patchContexts = new Dictionary<string, PatchContext>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var groups = PatchShared.BuildGroups(items, defaultPatchPath, patchContexts, target, log, ct);
            return await WriteAsync(groups, outputZipPath, target, log, progress, ct);
        }
        finally
        {
            foreach (var patchCtx in patchContexts.Values)
                patchCtx.Accessor.Dispose();
        }
    }

    internal static async Task<MergeResult> WriteAsync(List<MergeGroup> groups, string outputZipPath, OutputTarget target, Action<string, LogLevel> log, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        long totalBytes = 0;

        foreach (var g in groups)
            totalBytes += g.Targets.Sum(t => t.EstimatedSize) + GetWriteTotal(g, target);

        var reporter = new ProgressReporter("패치 적용 및 압축 중", string.Empty, totalBytes, progress);
        var writtenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int patchCandidates = 0;
        int patchedSuccess = 0;

        async Task<byte[]?> PatchAsync(MergeGroup group, PatchTarget patchTarget)
        {
            try
            {
                byte[]? bytes = await PatchShared.ResolveTargetBytesAsync(patchTarget, log, ct);

                if (bytes is not null)
                {
                    patchCandidates++;
                    patchedSuccess++;
                }

                return bytes;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                patchCandidates++;

                log($"[{group.Category}] {patchTarget.RelativePath}: 패치 실패, 원본 유지 - {ex.Message}", LogLevel.Error);

                return null;
            }
            finally
            {
                reporter.AddProgress(patchTarget.EstimatedSize);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);

        using (var zipStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var targetByPath = group.Targets.ToDictionary(t => t.RelativePath, StringComparer.OrdinalIgnoreCase);
                var handledTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (target == OutputTarget.Emu)
                {
                    foreach (var (relativePath, appEntry) in group.Index)
                    {
                        ct.ThrowIfCancellationRequested();

                        byte[]? outputBytes = targetByPath.TryGetValue(relativePath, out var patchTarget) ? await PatchAsync(group, patchTarget) : null;

                        if (outputBytes is null)
                        {
                            try
                            {
                                outputBytes = NoNpDrmDecryptor.DecryptEntry(appEntry.Owner.Item.Accessor!, appEntry.Owner.Item.SourcePath, appEntry.Owner.License.Klicensee, appEntry.FileEntry, appEntry.Owner.Table.UnicvEntries[appEntry.EntryIndex], appEntry.Owner.Table.FilesSalt);
                            }
                            catch (Exception ex)
                            {
                                log($"[{group.Category}] {relativePath}: 복호화 실패 - {ex.Message}", LogLevel.Error);
                                continue;
                            }
                        }

                        string entryPath = PatchShared.BuildEntryPath(PatchShared.GetBasePrefix(group.Category), group, relativePath);

                        await WriteZipEntryAsync(zip, writtenEntries, entryPath, outputBytes, appEntry.FileEntry.Size, reporter, ct);
                    }
                }
                else
                {
                    foreach (var owner in group.Owners)
                    {
                        foreach (var (relativePath, size) in PatchShared.GetAllOwnedFiles(owner))
                        {
                            ct.ThrowIfCancellationRequested();

                            if (!await WriteOwnedFileAsync(zip, writtenEntries, owner, group, relativePath, size, reporter, log, ct))
                                continue;

                            if (!targetByPath.TryGetValue(relativePath, out var patchTarget))
                                continue;

                            if (!handledTargets.Add(relativePath))
                                continue;

                            byte[]? patchedBytes = await PatchAsync(group, patchTarget);

                            if (patchedBytes != null)
                            {
                                string patchedEntryPath = PatchShared.BuildEntryPath(PatchShared.GetPatchedPrefix(group.Category, target), group, relativePath);

                                await WriteZipEntryAsync(zip, writtenEntries, patchedEntryPath, patchedBytes, patchTarget.EstimatedSize, reporter, ct);
                            }
                        }
                    }
                }

                if (target == OutputTarget.Emu)
                {
                    foreach (var owner in group.Owners)
                        PatchShared.WriteLicenseEntry(zip, writtenEntries, owner);
                }
            }

            reporter.ForceReport();
        }

        return new MergeResult { TotalFiles = writtenEntries.Count, PatchCandidates = patchCandidates, PatchedSuccessfully = patchedSuccess };
    }

    private static long GetWriteTotal(MergeGroup group, OutputTarget target)
    {
        if (target == OutputTarget.Emu)
            return group.Index.Values.Sum(e => (long)e.FileEntry.Size);

        long ownedTotal = 0;

        foreach (var owner in group.Owners)
            ownedTotal += PatchShared.GetAllOwnedFiles(owner).Sum(f => f.Size);

        return ownedTotal + group.Targets.Sum(t => t.EstimatedSize);
    }

    private static async Task<bool> WriteOwnedFileAsync(ZipArchive zip, HashSet<string> writtenEntries, OwnerContext owner, MergeGroup group, string relativePath, long size, ProgressReporter reporter, Action<string, LogLevel> log, CancellationToken ct)
    {
        string srcRel = $"{owner.Item.SourcePath}/{relativePath}";
        byte[] rawBytes;

        try
        {
            rawBytes = owner.Item.Accessor!.ReadAllBytes(srcRel);
        }
        catch (Exception ex)
        {
            log($"[{owner.Item.Category}] {relativePath}: 원본 읽기 실패 - {ex.Message}", LogLevel.Error);
            return false;
        }

        string baseEntryPath = PatchShared.BuildEntryPath(PatchShared.GetRetailBaseFolder(owner.Item.Category), group, relativePath);

        await WriteZipEntryAsync(zip, writtenEntries, baseEntryPath, rawBytes, size, reporter, ct);

        return true;
    }

    private static async Task WriteZipEntryAsync(ZipArchive zip, HashSet<string> writtenEntries, string entryPath, byte[] data, long estimatedSize, ProgressReporter reporter, CancellationToken ct)
    {
        if (!writtenEntries.Add(entryPath))
            return;

        var zipEntry = zip.CreateEntry(entryPath, CompressionLevel.NoCompression);

        await PatchShared.WriteEntryWithProgressAsync(zipEntry, data, estimatedSize, reporter, ct);
    }
}