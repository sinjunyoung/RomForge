using LibHac.Common;
using Common.Localization;
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

    public string ExtractExeFs(Nca baseProg, Nca? updateProg, string dirName, string outDir, ProgressContext ctx, CancellationToken ct = default)
    {
        string dir = Path.Combine(outDir, dirName);
        using var fs = OpenExeFsFileSystem(baseProg, updateProg);

        ExtractFileSystem(fs, dir, "ExeFS", ctx, ct);

        return dir;
    }

    private static IFileSystem OpenExeFsFileSystem(Nca baseProg, Nca? updateProg) => updateProg != null ? baseProg.OpenFileSystemWithPatch(updateProg, NcaSectionType.Code, IntegrityCheckLevel.None) : baseProg.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);

    private static IFileSystem OpenManualFileSystem(Nca? baseNca, Nca? updateNca) => baseNca != null && updateNca != null ? baseNca.OpenFileSystemWithPatch(updateNca, NcaSectionType.Data, IntegrityCheckLevel.None) : (baseNca ?? updateNca)!.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

    public static VirtualRomfs OpenVirtualExefs(Nca baseProg, Nca? updateProg) => VirtualRomfs.FromFileSystem(OpenExeFsFileSystem(baseProg, updateProg));

    public static VirtualRomfs? OpenVirtualLogo(Nca baseProg) => baseProg.CanOpenSection(NcaSectionType.Logo) ? VirtualRomfs.FromFileSystem(baseProg.OpenFileSystem(NcaSectionType.Logo, IntegrityCheckLevel.None)) : null;

    public static VirtualRomfs? OpenVirtualControl(Nca? controlNca, Nca? updateControlNca)
    {
        if (controlNca == null)
            return null;

        var control = VirtualRomfs.FromFileSystem(controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None));

        if (updateControlNca != null)
            control.MergeFileSystem(updateControlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None));

        return control;
    }

    public static VirtualRomfs? OpenVirtualManual(Nca? baseNca, Nca? updateNca, string label)
    {
        if (baseNca == null && updateNca == null)
            return null;

        try
        {
            return VirtualRomfs.FromFileSystem(OpenManualFileSystem(baseNca, updateNca));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{label} 열기 실패: {ex.Message}");
            return null;
        }
    }

    private static void EnsureSparseHasUpdate(Nca baseProg, Nca? updateProg)
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

    public static VirtualRomfs OpenVirtualRomfs(Nca baseProg, Nca? updateProg, bool isCreateOnly = false)
    {
        if (!isCreateOnly)
            EnsureSparseHasUpdate(baseProg, updateProg);

        var fs = updateProg != null ? baseProg.OpenFileSystemWithPatch(updateProg, NcaSectionType.Data, IntegrityCheckLevel.None) : baseProg.IsPatch ? baseProg.OpenFileSystemWithoutBase(NcaSectionType.Data, IntegrityCheckLevel.None) : baseProg.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
        return VirtualRomfs.FromFileSystem(fs);
    }

    public static void RegisterDlcStreams(List<Nca> dlcNcas, UnpackResult result)
    {
        foreach (var nca in dlcNcas)
        {
            if (!nca.CanOpenSection(NcaSectionType.Data))
                continue;

            string titleIdStr = nca.Header.TitleId.ToString("X16");

            try
            {
                var virtualRomfs = VirtualRomfs.FromFileSystem(nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None));

                result.Resources.Add(virtualRomfs);
                result.Dlcs.Add(new DlcUnpackInfo
                {
                    TitleId = nca.Header.TitleId,
                    Virtual = virtualRomfs,
                    RomfsOpener = virtualRomfs.OpenImage,
                    SourceCompressed = RomfsCompression.HasCompressedSection(nca)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DLC 열기 실패 ({titleIdStr}): {ex.Message}");
            }
        }
    }

    public string ExtractRomFs(Nca baseProg, Nca? updateProg, string dirName, string outDir, ProgressContext ctx, bool isCreateOnly = false, CancellationToken ct = default)
    {
        string dir = Path.Combine(outDir, dirName);

        if (!isCreateOnly)
            EnsureSparseHasUpdate(baseProg, updateProg);

        using var fs = updateProg != null ? baseProg.OpenFileSystemWithPatch(updateProg, NcaSectionType.Data, IntegrityCheckLevel.None) 
            : baseProg.IsPatch ? baseProg.OpenFileSystemWithoutBase(NcaSectionType.Data, IntegrityCheckLevel.None) : baseProg.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "RomFS", ctx, ct);

        return dir;
    }

    public string? ExtractLogo(Nca baseProg, string dirName, string outDir, ProgressContext ctx, CancellationToken ct = default)
    {
        if (!baseProg.CanOpenSection(NcaSectionType.Logo))
            return null;

        string dir = Path.Combine(outDir, dirName);
        using var fs = baseProg.OpenFileSystem(NcaSectionType.Logo, IntegrityCheckLevel.None);

        ExtractFileSystem(fs, dir, "Logo", ctx, ct);

        return dir;
    }

    public string? ExtractControl(Nca? controlNca, Nca? updateControlNca, BuildRequest req, UnpackResult result, string dirName, string outDir, string baseNspPath, ProgressContext ctx, CancellationToken ct = default)
    {
        if (controlNca == null)
            return null;

        string dir = Path.Combine(outDir, dirName);

        using (var fs = controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None))
            ExtractFileSystem(fs, dir, "Control (Base)", ctx, ct);

        if (updateControlNca != null)
        {
            using var updateFs = updateControlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

            ExtractFileSystem(updateFs, dir, "Control (Update)", ctx, ct);
        }

        ApplyTitleMetadata(req, result, baseNspPath);

        return dir;
    }

    public void ApplyTitleMetadata(BuildRequest req, UnpackResult result, string baseNspPath)
    {
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
    }

    public string? ExtractHtmlDoc(Nca? baseHtml, Nca? updateHtml, string dirName, string outDir, ProgressContext ctx, CancellationToken ct = default)
    {
        if (baseHtml == null && updateHtml == null)
            return null;

        string dir = Path.Combine(outDir, dirName);

        try
        {
            using var fs = OpenManualFileSystem(baseHtml, updateHtml);

            ExtractFileSystem(fs, dir, "HtmlDoc", ctx, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HtmlDoc 추출 실패 ({dirName}): {ex.Message}");
            return null;
        }

        return dir;
    }

    public string? ExtractLegal(Nca? baseLegal, Nca? updateLegal, string dirName, string outDir, ProgressContext ctx, CancellationToken ct = default)
    {
        if (baseLegal == null && updateLegal == null)
            return null;

        string dir = Path.Combine(outDir, dirName);
        using var fs = OpenManualFileSystem(baseLegal, updateLegal);

        ExtractFileSystem(fs, dir, "Legal", ctx, ct);

        return dir;
    }

    public void ExtractDlcs(List<Nca> dlcNcas, string outDir, UnpackResult result, ProgressContext ctx, CancellationToken ct = default)
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

                ExtractFileSystem(fs, dlcDestDir, $"DLC ({titleIdStr})", ctx, ct);
                result.Dlcs.Add(new DlcUnpackInfo { TitleId = nca.Header.TitleId, Dir = Path.Combine("DLCs", titleIdStr) });

                if (RomfsCompression.HasCompressedSection(nca))
                    File.WriteAllText(Path.Combine(dlcBaseDir, titleIdStr, RomfsCompression.FlagFileName), string.Empty);
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

    public void ExtractFileSystem(IFileSystem fs, string outDir, string label, ProgressContext ctx, CancellationToken ct = default)
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

            ctx.Reporter.Label = Loc.S.ProgressUnpackingNcaEntry(label, entry.Name);

            while ((read = srcStream.Read(buf, 0, buf.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                dest.Write(buf, 0, read);
                ctx.Reporter.AddProgress(read);
            }
        }
    }
}