using Patch.Core;
using System.IO.Compression;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class VitaPatchOnlyBuilder
{
    private static readonly HashSet<string> PatchExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xdelta", ".xdelta3", ".ips", ".ups", ".bps", ".ppf", ".aps" };

    public static async Task<VitaPatchOnlyResult> BuildAsync(string sourcePath, string patchPath, string outputZipPath, VitaOutputTarget target, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var source = VitaSourceAccessorFactory.Open(sourcePath);
        using var patch = VitaSourceAccessorFactory.Open(patchPath);
        var patchFiles = patch.EnumerateAllFiles()
            .Where(f => PatchExtensions.Contains(Path.GetExtension(f)))
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f)!, f => f, StringComparer.OrdinalIgnoreCase);
        var items = VitaSourcePreparer.DiscoverItems(source);
        var workBinReader = new WorkBinReader();
        var messages = new List<string>();
        int matched = 0;
        int success = 0;

        Directory.CreateDirectory(Path.GetDirectoryName(outputZipPath)!);

        using var zipStream = new FileStream(outputZipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            string workBinRel = $"{item.SourcePath}/sce_sys/package/work.bin";

            if (!source.FileExists(workBinRel))
            {
                messages.Add($"{item.Category} {item.TitleId}: work.bin 없음, 건너뜀");
                continue;
            }

            var license = WorkBinReader.Read(source, workBinRel);
            var table = VitaNoNpDrmDecryptor.ParseFileTable(source, item.SourcePath);

            for (int i = 0; i < table.Entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = table.Entries[i];

                if (entry.Type.IsDirectory())
                    continue;

                string relativePath = entry.RelativePath ?? entry.Name;
                string baseName = Path.GetFileName(relativePath);

                if (!patchFiles.TryGetValue(baseName, out var patchFileRel))
                    continue;

                matched++;

                string srcRel = $"{item.SourcePath}/{relativePath.Replace('\\', '/')}";

                if (!source.FileExists(srcRel))
                {
                    messages.Add($"{relativePath}: 원본 파일 없음");
                    continue;
                }

                try
                {
                    byte[] sourceBytes = VitaNoNpDrmDecryptor.DecryptEntry(source, item.SourcePath, license.Klicensee, entry, table.UnicvEntries[i], out string? warning);

                    if (warning != null)
                        messages.Add(warning);

                    byte[] patchBytes = patch.ReadAllBytes(patchFileRel);
                    byte[] patchedBytes = await UniversalPatcher.ApplyPatchAsync(sourceBytes, patchBytes, ct: ct);
                    string prefix = GetPrefix(item.Category, target);
                    string entryPath = item.Category == VitaContentCategory.Addcont ? $"{prefix}/{item.TitleId}/{item.ContentIdSuffix}/{relativePath}" : $"{prefix}/{item.TitleId}/{relativePath}";
                    var zipEntry = zip.CreateEntry(entryPath.Replace('\\', '/'), CompressionLevel.Optimal);

                    using (var entryStream = zipEntry.Open())
                        await entryStream.WriteAsync(patchedBytes, ct);

                    success++;
                    messages.Add($"{relativePath}: 패치 성공");
                }
                catch (Exception ex)
                {
                    messages.Add($"{relativePath}: 패치 실패 - {ex.Message}");
                }

                progress?.Report(matched == 0 ? 0 : (double)success / matched);
            }

            if (WorkBinReader.TryGetTitleIdFromContentId(license.ContentId, out string licenseTitleId))
            {
                var licenseEntry = zip.CreateEntry($"license/{licenseTitleId}/{license.ContentId}.rif", CompressionLevel.Optimal);
                byte[] workBinBytes = source.ReadAllBytes(workBinRel);
                using var es = licenseEntry.Open();

                await es.WriteAsync(workBinBytes, ct);
            }
        }

        return new VitaPatchOnlyResult { MatchedCandidates = matched, PatchedSuccessfully = success, Messages = messages };
    }

    private static string GetPrefix(VitaContentCategory category, VitaOutputTarget target) => (category, target) switch
    {
        (VitaContentCategory.App, VitaOutputTarget.Emu) => "app",
        (VitaContentCategory.Patch, VitaOutputTarget.Emu) => "app",
        (VitaContentCategory.Addcont, VitaOutputTarget.Emu) => "addcont",
        (VitaContentCategory.App, VitaOutputTarget.Retail) => "app",
        (VitaContentCategory.Patch, VitaOutputTarget.Retail) => "rePatch",
        (VitaContentCategory.Addcont, VitaOutputTarget.Retail) => "reAddcont",
        _ => throw new NotSupportedException()
    };
}