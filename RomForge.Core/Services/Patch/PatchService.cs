using Common;
using Patch.Core;
using RomForge.Core.Models.Patch;
using System.IO;
using System.IO.Compression;

namespace RomForge.Core.Services.Patch;

public static class PatchService
{
    public static async Task ApplyPatchedZipAsync(string sourceZipPath, string outputZipPath, IReadOnlyDictionary<string, PatchEntry> patchesByEntryName, IProgress<EntryPatchProgress>? progress = null, CancellationToken ct = default)
    {
        var outputDir = Path.GetDirectoryName(outputZipPath);

        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var openPatchZips = new Dictionary<string, ZipArchive>();

        try
        {
            using var sourceZip = ZipFile.OpenRead(sourceZipPath);
            using var outputStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
            using var outputZip = new ZipArchive(outputStream, ZipArchiveMode.Create);

            foreach (var sourceEntry in sourceZip.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(sourceEntry.Name))
                {
                    outputZip.CreateEntry(sourceEntry.FullName);
                    continue;
                }

                if (patchesByEntryName.TryGetValue(sourceEntry.FullName, out var patchEntry))
                    await WritePatchedEntryAsync(sourceEntry, patchEntry, outputZip, openPatchZips, progress, ct);
                else
                    await CopyEntryAsync(sourceEntry, outputZip, ct);
            }
        }
        finally
        {
            foreach (var zip in openPatchZips.Values)
                zip.Dispose();
        }
    }

    private static async Task WritePatchedEntryAsync(ZipArchiveEntry sourceEntry, PatchEntry patchEntry, ZipArchive outputZip, Dictionary<string, ZipArchive> openPatchZips, IProgress<EntryPatchProgress>? progress, CancellationToken ct = default)
    {
        byte[] sourceBytes;

        using (var srcStream = sourceEntry.Open())
        using (var ms = new MemoryStream())
        {
            await srcStream.CopyToAsync(ms, ct);
            sourceBytes = ms.ToArray();
        }

        var patchBytes = await ReadPatchBytesAsync(patchEntry, openPatchZips, ct);

        ct.ThrowIfCancellationRequested();

        var resultBytes = await
            UniversalPatcher.ApplyPatchAsync(sourceBytes, patchBytes, p =>
                progress?.Report(new EntryPatchProgress { EntryName = sourceEntry.FullName, Percent = (int)(p * 100) }), ct);

        var newEntry = outputZip.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);

        using (var destStream = newEntry.Open())
            await destStream.WriteAsync(resultBytes, ct);

        progress?.Report(new EntryPatchProgress { EntryName = sourceEntry.FullName, Percent = 100 });
    }

    private static async Task CopyEntryAsync(ZipArchiveEntry sourceEntry, ZipArchive outputZip, CancellationToken ct)
    {
        var newEntry = outputZip.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);

        using var srcStream = sourceEntry.Open();
        using var destStream = newEntry.Open();

        await srcStream.CopyToAsync(destStream, ct);
    }

    private static async Task<byte[]> ReadPatchBytesAsync(PatchEntry patchEntry, Dictionary<string, ZipArchive> openPatchZips, CancellationToken ct)
    {
        if (!patchEntry.IsZipEntry)
            return await File.ReadAllBytesAsync(patchEntry.EntryPath, ct);

        if (!openPatchZips.TryGetValue(patchEntry.ZipPath!, out var zip))
        {
            zip = ZipFile.OpenRead(patchEntry.ZipPath!);
            openPatchZips[patchEntry.ZipPath!] = zip;
        }

        var entry = zip.GetEntry(patchEntry.EntryPath)
            ?? throw new FileNotFoundException($"ZIP 없음: {patchEntry.EntryPath}");

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        return ms.ToArray();
    }
}