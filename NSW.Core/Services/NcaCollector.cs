using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core.Models;
using System.Diagnostics;
using System.IO;
using ContentType = LibHac.Ncm.ContentType;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public sealed record CollectedNcas(
    Dictionary<byte, Nca> BaseProgs, Dictionary<byte, Nca> UpdateProgs, Dictionary<byte, Nca> BaseControls, Dictionary<byte, Nca> UpdateControls, Dictionary<byte, Nca> BaseHtmls,
    Dictionary<byte, Nca> UpdateHtmls, Dictionary<byte, Nca> BaseLegals, Dictionary<byte, Nca> UpdateLegals, List<Nca> DlcNcas, uint PatchVersion, HashSet<byte> CreateOnlyOffsets);

public sealed class NcaCollector(KeySet keySet)
{
    private readonly KeySet _keySet = keySet;
    private readonly List<Stream> _nscStreams = [];

    public IReadOnlyList<Stream> NscStreams => _nscStreams;

    public CollectedNcas Collect(List<IFileSystem> partitions, List<string> sourcePaths, BuildRequest req)
    {
        var baseProgIds = new Dictionary<byte, string>();
        var updateProgIds = new Dictionary<byte, string>();
        var baseControlIds = new Dictionary<byte, string>();
        var updateControlIds = new Dictionary<byte, string>();
        var baseHtmlIds = new Dictionary<byte, string>();
        var updateHtmlIds = new Dictionary<byte, string>();
        var baseLegalIds = new Dictionary<byte, string>();
        var updateLegalIds = new Dictionary<byte, string>();
        var dlcNcaIds = new HashSet<string>();
        uint patchVersion = 0;
        var deltaToBaseNcaId = new Dictionary<string, string>();
        var createNcaIds = new HashSet<string>();
        var createOnlyOffsets = new HashSet<byte>();
        var processedIds = new HashSet<string>();
        var allNcas = new Dictionary<string, Nca>();
        var cnmtList = new List<LibHac.Tools.Ncm.Cnmt>();
        bool filtering = req.DlcTitleIdsByFile != null;

        for (int i = 0; i < partitions.Count; i++)
        {
            var pfs = partitions[i];
            string sourcePath = sourcePaths[i];

            foreach (var entry in EnumerateNcaEntries(pfs))
            {
                string currentId = Path.GetFileNameWithoutExtension(entry.Name).ToLower();
                bool isNew = processedIds.Add(currentId);

                if (!isNew && !filtering)
                    continue;

                var nca = OpenNca(pfs, entry.FullPath);

                if (nca == null)
                    continue;

                if (isNew)
                    allNcas[currentId] = nca;

                if (nca.Header.ContentType != NcaContentType.Meta)
                    continue;

                try
                {
                    using var metaFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                    cnmtList.AddRange(ReadCnmtsFromFs(metaFs).Where(c => IsAllowedCnmt(c, sourcePath, req)));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CNMT 파싱 실패 ({entry.FullPath}): {ex.Message}");
                }
            }
        }

        foreach (var cnmt in cnmtList)
        {
            if (cnmt.Type == ContentMetaType.Patch)
            {
                patchVersion = cnmt.TitleVersion.Version;

                if (cnmt.ExtendedData?.FragmentSets != null)
                {
                    foreach (var fs in cnmt.ExtendedData.FragmentSets)
                    {
                        if (fs.Type != ContentType.Program)
                            continue;

                        string newId = Convert.ToHexString(fs.NcaIdNew).ToLowerInvariant();
                        string oldId = Convert.ToHexString(fs.NcaIdOld).ToLowerInvariant();

                        if (fs.DeltaType == UpdateType.Create)
                            createNcaIds.Add(newId);
                        else
                            deltaToBaseNcaId[newId] = oldId;
                    }
                }
            }

            foreach (var entry in cnmt.ContentEntries)
            {
                string ncaId = Convert.ToHexString(entry.NcaId).ToLowerInvariant();
                byte idOffset = entry.IdOffset;

                switch (cnmt.Type)
                {
                    case ContentMetaType.Application:
                        switch (entry.Type)
                        {
                            case ContentType.Program:
                                baseProgIds[idOffset] = ncaId;
                                break;
                            case ContentType.Control:
                                baseControlIds[idOffset] = ncaId;
                                break;
                            case ContentType.HtmlDocument:
                                baseHtmlIds[idOffset] = ncaId;
                                break;
                            case ContentType.LegalInformation:
                                baseLegalIds[idOffset] = ncaId;
                                break;
                        }
                        break;
                    case ContentMetaType.Patch:
                        switch (entry.Type)
                        {
                            case ContentType.Program:
                                updateProgIds[idOffset] = ncaId;
                                break;
                            case ContentType.Control:
                                updateControlIds[idOffset] = ncaId;
                                break;
                            case ContentType.HtmlDocument:
                                updateHtmlIds[idOffset] = ncaId;
                                break;
                            case ContentType.LegalInformation:
                                updateLegalIds[idOffset] = ncaId;
                                break;
                        }
                        break;
                    case ContentMetaType.AddOnContent:
                        dlcNcaIds.Add(ncaId);
                        break;
                }
            }
        }

        var baseProgReverse = baseProgIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var updateProgReverse = updateProgIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var baseControlReverse = baseControlIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var updateControlReverse = updateControlIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var baseHtmlReverse = baseHtmlIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var updateHtmlReverse = updateHtmlIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var baseLegalReverse = baseLegalIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var updateLegalReverse = updateLegalIds.ToDictionary(kv => kv.Value, kv => kv.Key);
        var baseProgs = new Dictionary<byte, Nca>();
        var updateProgs = new Dictionary<byte, Nca>();
        var baseControls = new Dictionary<byte, Nca>();
        var updateControls = new Dictionary<byte, Nca>();
        var baseHtmls = new Dictionary<byte, Nca>();
        var updateHtmls = new Dictionary<byte, Nca>();
        var baseLegals = new Dictionary<byte, Nca>();
        var updateLegals = new Dictionary<byte, Nca>();
        var dlcNcas = new List<Nca>();

        foreach (var (currentId, nca) in allNcas)
        {
            if (baseProgReverse.TryGetValue(currentId, out var bpOffset))
                baseProgs[bpOffset] = nca;
            else if (updateProgReverse.TryGetValue(currentId, out var upOffset))
            {
                if (!baseProgIds.ContainsKey(upOffset))
                {
                    baseProgs[upOffset] = nca;
                    createOnlyOffsets.Add(upOffset);
                }
                else if (deltaToBaseNcaId.TryGetValue(currentId, out var targetBaseId) && baseProgReverse.TryGetValue(targetBaseId, out var realBaseOffset))
                    updateProgs[realBaseOffset] = nca;
                else
                    updateProgs[upOffset] = nca;
            }
            else if (baseControlReverse.TryGetValue(currentId, out var bcOffset))
                baseControls[bcOffset] = nca;
            else if (updateControlReverse.TryGetValue(currentId, out var ucOffset))
                updateControls[ucOffset] = nca;
            else if (baseHtmlReverse.TryGetValue(currentId, out var bhOffset))
                baseHtmls[bhOffset] = nca;
            else if (updateHtmlReverse.TryGetValue(currentId, out var uhOffset))
                updateHtmls[uhOffset] = nca;
            else if (baseLegalReverse.TryGetValue(currentId, out var blOffset))
                baseLegals[blOffset] = nca;
            else if (updateLegalReverse.TryGetValue(currentId, out var ulOffset))
                updateLegals[ulOffset] = nca;
            else if (dlcNcaIds.Contains(currentId))
                dlcNcas.Add(nca);
        }

        return new CollectedNcas(baseProgs, updateProgs, baseControls, updateControls, baseHtmls, updateHtmls, baseLegals, updateLegals, dlcNcas, patchVersion, createOnlyOffsets);
    }

    private static bool IsAllowedCnmt(LibHac.Tools.Ncm.Cnmt cnmt, string sourcePath, BuildRequest req)
    {
        if (req.DlcTitleIdsByFile == null)
            return true;

        return cnmt.Type switch
        {
            ContentMetaType.Application => string.Equals(sourcePath, req.BaseFilePath, StringComparison.OrdinalIgnoreCase),
            ContentMetaType.Patch => string.Equals(sourcePath, req.UpdateFilePath, StringComparison.OrdinalIgnoreCase),
            ContentMetaType.AddOnContent => req.DlcTitleIdsByFile.TryGetValue(sourcePath, out var ids) && ids.Contains(cnmt.TitleId.ToString("X16")),
            _ => true
        };
    }

    private static IEnumerable<LibHac.Tools.Ncm.Cnmt> ReadCnmtsFromFs(IFileSystem metaFs)
    {
        foreach (var cnmtEntry in metaFs.EnumerateEntries("/", "*.cnmt"))
        {
            using var cnmtFile = new UniqueRef<IFile>();

            if (metaFs.OpenFile(ref cnmtFile.Ref, cnmtEntry.FullPath.ToU8Span(), OpenMode.Read).IsFailure())
                continue;

            yield return new LibHac.Tools.Ncm.Cnmt(cnmtFile.Get.AsStream());
        }
    }

    private static IEnumerable<DirectoryEntryEx> EnumerateNcaEntries(IFileSystem pfs) => pfs.EnumerateEntries("/", "*.nc*")
        .Where(e => e.Type == DirectoryEntryType.File && (e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)));

    private Nca? OpenNca(IFileSystem pfs, string fullPath)
    {
        try
        {
            var file = new UniqueRef<IFile>();

            if (pfs.OpenFile(ref file.Ref, fullPath.ToU8Span(), OpenMode.Read).IsFailure())
                return null;

            Stream baseStream = file.Release().AsStream();

            _nscStreams.Add(baseStream);

            if (fullPath.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                return new Ncz(_keySet, baseStream, NczReadMode.Fast);
            else
                return new Nca(_keySet, baseStream.AsStorage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NCA 열기 실패 ({fullPath}): {ex.Message}");
            return null;
        }
    }
}