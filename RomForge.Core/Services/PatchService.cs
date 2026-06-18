using Common;
using Patch.Core;
using RomForge.Core.Models;
using System.IO;
using System.IO.Compression;

namespace RomForge.Core.Services;

public static class PatchService
{
    public static async Task ApplyAsync(
        SourceEntry source,
        PatchEntry patch,
        string outputDir,
        IProgress<ProgressInfo>? progress = null,
        Action<string, LogLevel>? log = null,
        CancellationToken ct = default)
    {
        var sourceBytes = source.IsZipEntry
            ? await ReadZipEntryAsync(source.ZipPath!, source.EntryPath, ct)
            : await File.ReadAllBytesAsync(source.EntryPath, ct);

        var patchBytes = patch.IsZipEntry
            ? await ReadZipEntryAsync(patch.ZipPath!, patch.EntryPath, ct)
            : await File.ReadAllBytesAsync(patch.EntryPath, ct);

        ct.ThrowIfCancellationRequested();

        var result = await Task.Run(() =>
            UniversalPatcher.ApplyPatch(sourceBytes, patchBytes,
                p => progress?.Report(new ProgressInfo { Percent = (int)(p * 100) })), ct);

        Directory.CreateDirectory(outputDir);

        if (source.IsZipEntry)
        {
            // 원본 ZIP 을 output 에 복사 (없으면) 후 엔트리 교체
            string outputZipPath = Path.Combine(outputDir,
                Path.GetFileName(source.ZipPath!));

            if (!File.Exists(outputZipPath))
                File.Copy(source.ZipPath!, outputZipPath);

            await WriteZipEntryAsync(outputZipPath, source.EntryPath, result, ct);
        }
        else
        {
            string outputPath = Path.Combine(outputDir, source.DisplayName);
            await File.WriteAllBytesAsync(outputPath, result, ct);
        }

        log?.Invoke($"[{source.DisplayName}] 완료", LogLevel.Ok);
    }

    private static async Task<byte[]> ReadZipEntryAsync(string zipPath, string entryPath, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(entryPath)
            ?? throw new FileNotFoundException($"ZIP 엔트리 없음: {entryPath}");
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static async Task WriteZipEntryAsync(string zipPath, string entryName, byte[] data, CancellationToken ct)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        var existing = zip.GetEntry(entryName);
        existing?.Delete();
        var newEntry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = newEntry.Open();
        await stream.WriteAsync(data, ct);
    }
}