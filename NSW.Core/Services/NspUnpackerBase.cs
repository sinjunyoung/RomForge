using Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core.Models;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public abstract class NspUnpackerBase(KeySet keySet)
{
    protected readonly KeySet _keySet = keySet;

    public string BaseNspPath { get; private set; } = string.Empty;

    protected async Task<UnpackResult> UnpackCore(BuildRequest req, string outDir, bool withControlNca, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        BaseNspPath = req.BaseFilePath;

        Directory.CreateDirectory(outDir);

        var storages = new List<LocalStorage>();
        var partitions = new List<IFileSystem>();
        var collector = new NcaCollector(_keySet);

        try
        {
            var allPaths = new List<string> { req.BaseFilePath };

            if (!string.IsNullOrEmpty(req.UpdateFilePath))
                allPaths.Add(req.UpdateFilePath);

            allPaths.AddRange(req.DlcFilePaths);

            var uniquePaths = allPaths.Distinct().Where(File.Exists).ToList();

            foreach (var path in uniquePaths)
            {
                var storage = new LocalStorage(path, FileAccess.Read);

                storages.Add(storage);

                var pfs = storage.OpenFileSystem(_keySet, path);

                partitions.Add(pfs);
                _keySet.RegisterTickets(pfs);
            }

            var ncas = collector.Collect(partitions, uniquePaths, req);

            if (!ncas.BaseProgs.ContainsKey(0))
                throw new Exception("Base Program NCA를 찾을 수 없습니다.");

            var extractor = new NcaExtractor(_keySet, ShouldExtract);
            var progCtx = CreateProgressContext(uniquePaths, ncas);

            progCtx.Reporter = new ProgressReporter(string.Empty, string.Empty, progCtx.TotalSize, progress);

            var result = BuildBaseResult(ncas);
            var allOffsets = ncas.BaseProgs.Keys
                .Union(ncas.UpdateProgs.Keys)
                .OrderBy(k => k);

            foreach (var idOffset in allOffsets)
            {
                ncas.BaseProgs.TryGetValue(idOffset, out var baseProg);
                ncas.UpdateProgs.TryGetValue(idOffset, out var updateProg);

                if (baseProg == null && updateProg == null)
                    continue;

                ulong programTitleId = result.TitleId + idOffset;
                string programDir = Path.Combine(outDir, programTitleId.ToString("X16"));
                var effectiveProg = baseProg ?? updateProg!;
                var patchProg = baseProg != null ? updateProg : null;

                if (effectiveProg.CanOpenSection(NcaSectionType.Code))
                    result.ExefsDirs[idOffset] = extractor.ExtractExeFs(effectiveProg, patchProg, "exefs", programDir, progCtx, progress, ct);

                if (effectiveProg.CanOpenSection(NcaSectionType.Data))
                {
                    result.RomfsDirs[idOffset] = extractor.ExtractRomFs(effectiveProg, patchProg, "romfs", programDir, progCtx, ncas.CreateOnlyOffsets.Contains(idOffset), progress, ct);

                    if (HasCompressedSection(effectiveProg) || HasCompressedSection(patchProg))
                    {
                        File.WriteAllText(Path.Combine(programDir, "romfs.compressed"), string.Empty);
                        result.CompressedRomfs.Add(idOffset);
                    }
                }

                var logoDir = extractor.ExtractLogo(effectiveProg, "logo", programDir, progCtx, progress, ct);

                if (logoDir != null)
                    result.LogoDirs[idOffset] = logoDir;

                ncas.BaseControls.TryGetValue(idOffset, out var baseControl);
                ncas.UpdateControls.TryGetValue(idOffset, out var updateControl);

                var effectiveControl = baseControl ?? updateControl;
                var controlDir = extractor.ExtractControl(effectiveControl, baseControl != null ? updateControl : null, req, result, "control", programDir, BaseNspPath, progCtx, progress, ct);

                if (controlDir != null)
                    result.ControlDirs[idOffset] = controlDir;

                ncas.BaseHtmls.TryGetValue(idOffset, out var baseHtml);
                ncas.UpdateHtmls.TryGetValue(idOffset, out var updateHtml);

                var htmlDir = extractor.ExtractHtmlDoc(baseHtml, updateHtml, "htmldoc", programDir, progCtx, progress, ct);

                if (htmlDir != null)
                    result.HtmlDocDirs[idOffset] = htmlDir;

                ncas.BaseLegals.TryGetValue(idOffset, out var baseLegal);
                ncas.UpdateLegals.TryGetValue(idOffset, out var updateLegal);

                var legalDir = extractor.ExtractLegal(baseLegal, updateLegal, "legal", programDir, progCtx, progress, ct);

                if (legalDir != null)
                    result.LegalDirs[idOffset] = legalDir;
            }

            extractor.ExtractDlcs(ncas.DlcNcas, outDir, result, progCtx, progress, ct);

            if (withControlNca)
            {
                if (ncas.BaseControls.TryGetValue(0, out var mainControl))
                {
                    uint version = ncas.PatchVersion;
                    string fileName = version > 0 ? $"control_{version}.nca" : "control.nca";
                    string controlNcaPath = Path.Combine(outDir, fileName);
                    using var fs = new FileStream(controlNcaPath, FileMode.Create, FileAccess.Write);

                    mainControl.BaseStorage.CopyToStream(fs);
                }
            }

            progress.ReportStatus("완료", 100);

            return result;
        }
        finally
        {
            foreach (var s in storages)
                s.Dispose();

            foreach (var ns in collector.NscStreams)
                ns.Dispose();
        }
    }

    private static bool HasCompressedSection(Nca? nca)
    {
        if (nca == null)
            return false;

        for (int i = 0; i < 4; i++)
            if (nca.SectionExists(i) && nca.GetFsHeader(i).ExistsCompressionLayer())
                return true;

        return false;
    }

    protected abstract ProgressContext CreateProgressContext(List<string> inputPaths, CollectedNcas ncas);

    protected abstract bool ShouldExtract(string entryPath);

    private static UnpackResult BuildBaseResult(CollectedNcas ncas)
    {
        if (!ncas.BaseControls.TryGetValue(0, out var mainControl))
            throw new Exception("Control NCA를 찾을 수 없습니다.");

        var result = new UnpackResult
        {
            TitleId = mainControl.Header.TitleId,
            BaseSdkVersion = mainControl.Header.SdkVersion.Version,
            BaseKeyGeneration = mainControl.Header.KeyGeneration,
        };

        if (ncas.UpdateProgs.ContainsKey(0))
        {
            result.HasUpdate = true;
            result.GameVersion = ncas.PatchVersion;
        }

        return result;
    }

    protected long CalculateMatchedSize(CollectedNcas ncas)
    {
        long total = 0;

        void SumFs(IFileSystem fs)
        {
            foreach (var entry in fs.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
                if (entry.Type == DirectoryEntryType.File && ShouldExtract(entry.FullPath))
                    total += entry.Size;
        }

        void SumControlFsByPath(Nca? baseControl, Nca? updateControl)
        {
            var sizeByPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            void Collect(IFileSystem fs)
            {
                foreach (var entry in fs.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
                    if (entry.Type == DirectoryEntryType.File && ShouldExtract(entry.FullPath))
                        sizeByPath[entry.FullPath] = entry.Size;
            }

            NcaExtractor.TryWithFs(baseControl, null, NcaSectionType.Data, Collect);
            NcaExtractor.TryWithFs(updateControl, null, NcaSectionType.Data, Collect);

            total += sizeByPath.Values.Sum();
        }

        foreach (var (idOffset, baseProg) in ncas.BaseProgs)
        {
            ncas.UpdateProgs.TryGetValue(idOffset, out var updateProg);

            NcaExtractor.TryWithFs(baseProg, updateProg, NcaSectionType.Code, SumFs);
            NcaExtractor.TryWithFs(baseProg, updateProg, NcaSectionType.Data, SumFs);

            if (idOffset == 0 && baseProg.CanOpenSection(NcaSectionType.Logo))
                NcaExtractor.TryWithFs(baseProg, null, NcaSectionType.Logo, SumFs);
        }

        foreach (var (idOffset, baseControl) in ncas.BaseControls)
        {
            ncas.UpdateControls.TryGetValue(idOffset, out var updateControl);
            SumControlFsByPath(baseControl, updateControl);
        }

        foreach (var (idOffset, baseHtml) in ncas.BaseHtmls)
        {
            ncas.UpdateHtmls.TryGetValue(idOffset, out var updateHtml);
            NcaExtractor.TryWithFs(baseHtml, updateHtml, NcaSectionType.Data, SumFs);
        }

        foreach (var (idOffset, baseLegal) in ncas.BaseLegals)
        {
            ncas.UpdateLegals.TryGetValue(idOffset, out var updateLegal);
            NcaExtractor.TryWithFs(baseLegal, updateLegal, NcaSectionType.Data, SumFs);
        }

        foreach (var dlc in ncas.DlcNcas)
            NcaExtractor.TryWithFs(dlc, null, NcaSectionType.Data, SumFs);

        return total;
    }
}