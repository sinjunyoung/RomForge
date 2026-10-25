using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core.Models;
using System.Diagnostics;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public sealed class NcaExtractor(KeySet keySet, Func<string, bool> shouldExtract)
{
    private readonly KeySet _keySet = keySet;

    private readonly Func<string, bool> _shouldExtract = shouldExtract;

    private readonly byte[] _copyBuffer = new byte[0x1000000];

    public string ExtractExeFs(Nca baseProg, Nca? updateProg, string dirName, string outDir, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string dir = Path.Combine(outDir, dirName);
        using var fs = updateProg != null ? baseProg.OpenFileSystemWithPatch(updateProg, NcaSectionType.Code, IntegrityCheckLevel.None) : baseProg.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "ExeFS", ctx, progress, ct);

        return dir;
    }

    public string ExtractRomFs(Nca baseProg, Nca? updateProg, string dirName, string outDir, ProgressContext ctx, bool isCreateOnly = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string dir = Path.Combine(outDir, dirName);

        if (!isCreateOnly)
        {
            bool hasSparse = false;

            for (int i = 0; i < 4; i++)
            {
                if (!baseProg.SectionExists(i))
                    continue;

                if (baseProg.GetFsHeader(i).ExistsSparseLayer())
                {
                    hasSparse = true;
                    break;
                }
            }
            if (hasSparse && updateProg == null)
                throw new InvalidOperationException("SparseStorage NCA는 업데이트가 필요합니다.");
        }

        using var fs = updateProg != null ? baseProg.OpenFileSystemWithPatch(updateProg, NcaSectionType.Data, IntegrityCheckLevel.None)
            : baseProg.IsPatch ? baseProg.OpenFileSystemWithoutBase(NcaSectionType.Data, IntegrityCheckLevel.None) : baseProg.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "RomFS", ctx, progress, ct);

        return dir;
    }

    public string? ExtractLogo(Nca baseProg, string dirName, string outDir, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (!baseProg.CanOpenSection(NcaSectionType.Logo))
            return null;

        string dir = Path.Combine(outDir, dirName);
        using var fs = baseProg.OpenFileSystem(NcaSectionType.Logo, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "Logo", ctx, progress, ct);

        return dir;
    }

    public string? ExtractControl(Nca? controlNca, Nca? updateControlNca, BuildRequest req, UnpackResult result, string dirName, string outDir, string baseNspPath, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (controlNca == null)
            return null;

        string dir = Path.Combine(outDir, dirName);

        using (var fs = controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None))
            ExtractFileSystem(fs, dir, "Control (Base)", ctx, progress, ct);

        if (updateControlNca != null)
        {
            using var updateFs = updateControlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

            ExtractFileSystem(updateFs, dir, "Control (Update)", ctx, progress, ct);
        }

        string metaSourcePath = !string.IsNullOrEmpty(req.UpdateFilePath) ? req.UpdateFilePath : baseNspPath;
        var meta = MetadataReader.GetMetadataFromContainer(_keySet, metaSourcePath)
            .OrderByDescending(m => m.Type == LibHac.Ncm.ContentMetaType.Patch)
            .FirstOrDefault();

        if (meta != null)
        {
            result.KrTitle = meta.KrTitle;
            result.EnTitle = meta.EnTitle;
            result.DisplayVersion = meta.DisplayVersion;
        }

        return dir;
    }

    public string? ExtractHtmlDoc(Nca? baseHtml, Nca? updateHtml, string dirName, string outDir, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (baseHtml == null && updateHtml == null)
            return null;

        string dir = Path.Combine(outDir, dirName);

        try
        {
            using var fs = baseHtml != null && updateHtml != null ? baseHtml.OpenFileSystemWithPatch(updateHtml, NcaSectionType.Data, IntegrityCheckLevel.None) : (baseHtml ?? updateHtml)!.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

            ExtractFileSystem(fs, dir, "HtmlDoc", ctx, progress, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HtmlDoc 추출 실패 ({dirName}): {ex.Message}");
            return null;
        }

        return dir;
    }

    public string? ExtractLegal(Nca? baseLegal, Nca? updateLegal, string dirName, string outDir, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (baseLegal == null && updateLegal == null)
            return null;

        string dir = Path.Combine(outDir, dirName);
        using var fs = baseLegal != null && updateLegal != null ? baseLegal.OpenFileSystemWithPatch(updateLegal, NcaSectionType.Data, IntegrityCheckLevel.None) : (baseLegal ?? updateLegal)!.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "Legal", ctx, progress, ct);

        return dir;
    }

    public void ExtractDlcs(List<Nca> dlcNcas, string outDir, UnpackResult result, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (dlcNcas.Count == 0)
            return;

        string dlcBaseDir = Path.Combine(outDir, "DLCs");

        foreach (var nca in dlcNcas)
        {
            ct.ThrowIfCancellationRequested();

            string titleIdStr = nca.Header.TitleId.ToString("X16");
            string dlcDestDir = Path.Combine(dlcBaseDir, titleIdStr, "romfs");

            if (!nca.CanOpenSection(NcaSectionType.Data))
                continue;

            try
            {
                using var fs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                ExtractFileSystem(fs, dlcDestDir, $"DLC ({titleIdStr})", ctx, progress, ct);
                result.Dlcs.Add(new DlcUnpackInfo { TitleId = nca.Header.TitleId, Dir = Path.Combine("DLCs", titleIdStr) });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DLC 추출 실패 ({titleIdStr}): {ex.Message}");
            }
        }
    }

    public static void TryWithFs(Nca? nca, Nca? patchNca, NcaSectionType section, Action<IFileSystem> action)
    {
        if (nca == null)
            return;

        try
        {
            using var fs = patchNca != null ? nca.OpenFileSystemWithPatch(patchNca, section, IntegrityCheckLevel.None) : nca.OpenFileSystem(section, IntegrityCheckLevel.None);

            action(fs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"섹션 열기 실패 ({section}, TitleId={nca.Header.TitleId:X16}): {ex.Message}");
        }
    }

    public void ExtractFileSystem(IFileSystem fs, string outDir, string label, ProgressContext ctx, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var buf = _copyBuffer;

        foreach (var entry in fs.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
        {
            ct.ThrowIfCancellationRequested();

            if (!_shouldExtract(entry.FullPath))
                continue;

            string destPath = Path.Combine(outDir, entry.FullPath.TrimStart('/'));

            if (entry.Type == DirectoryEntryType.Directory)
            {
                Directory.CreateDirectory(destPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            using var src = new UniqueRef<IFile>();

            if (!fs.OpenFile(ref src.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                continue;

            using var dest = File.Open(destPath, FileMode.Create);
            var srcStream = src.Get.AsStream();
            int read;

            ctx.Reporter.Label = $"{label} 언팩 중 - {entry.Name}";

            while ((read = srcStream.Read(buf, 0, buf.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                dest.Write(buf, 0, read);
                ctx.Reporter.AddProgress(read);
            }
        }
    }
}