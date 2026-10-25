using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Models;
using RomForge.Core.Models.Switch;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public abstract class BaseSwitchService
{
    private const long XciHfs0HeaderSizePos = 0x138;
    private const long XciHfs0HeaderHashPos = 0x140;
    private const uint MediaSize = 0x200;

    private static readonly byte[] XciHeaderTemplate = [ 
        0x49, 0x04, 0x47, 0x85, 0x7D, 0x54, 0x74, 0xCA, 0xD5, 0x09, 0xB3, 0x6E, 0x81, 0xA9, 0xFE, 0x22, 0xAA, 0xBF, 0xE2, 0xA9, 0x78, 0xA3, 0x30, 0x1A, 0x9D, 0x0C, 0x2D, 0xEF, 0x27, 0xCF, 0x6D, 0x47,
        0x4D, 0xFE, 0x8A, 0xC2, 0xD5, 0xFD, 0xF4, 0x1D, 0xC8, 0xA2, 0xFA, 0xDF, 0xB6, 0xD2, 0xAD, 0x7D, 0x65, 0x6B, 0xEB, 0x63, 0x3B, 0x63, 0x8A, 0xE5, 0xE2, 0x5D, 0x21, 0xC1, 0x79, 0xAB, 0xCB, 0x1D,
        0x0C, 0x6B, 0x01, 0x6D, 0x29, 0xA6, 0xC8, 0x1F, 0xD5, 0xD2, 0x09, 0xAE, 0x3E, 0x67, 0x47, 0x71, 0x00, 0x73, 0xA3, 0xEA, 0xD9, 0x1A, 0x73, 0x15, 0xDC, 0x64, 0x34, 0xBF, 0xAE, 0xCD, 0x97, 0xD5,
        0x2E, 0x99, 0x4A, 0xBF, 0x72, 0xFE, 0x75, 0x04, 0x8A, 0x1E, 0x36, 0x65, 0x53, 0x79, 0x25, 0xEA, 0xB0, 0x07, 0x57, 0x16, 0xEC, 0x2A, 0x24, 0x11, 0xE0, 0x64, 0x76, 0x25, 0x1F, 0x35, 0xDF, 0x63,
        0xDB, 0xB4, 0xE2, 0x6C, 0xC3, 0x2F, 0x1A, 0xED, 0xB1, 0x01, 0x54, 0xEF, 0xA1, 0x21, 0xDF, 0xD7, 0x13, 0x21, 0xF1, 0xA1, 0xF2, 0xCF, 0x46, 0x0D, 0xD5, 0x68, 0x40, 0x6D, 0xC1, 0x4D, 0x4A, 0xDE,
        0x99, 0x08, 0x32, 0x8B, 0x81, 0x25, 0x50, 0xB4, 0xCD, 0x3C, 0x34, 0x1F, 0x61, 0xB8, 0x72, 0x44, 0x4B, 0x63, 0x84, 0x86, 0xEE, 0xCD, 0x9B, 0xC4, 0xB6, 0xD4, 0x7A, 0x71, 0x9F, 0x7E, 0xA7, 0x10,
        0xC9, 0xE9, 0xDB, 0x97, 0xCA, 0x2F, 0x15, 0xBC, 0xB9, 0x83, 0xEE, 0xBF, 0x1A, 0x5C, 0xFC, 0x4E, 0xFD, 0xF8, 0x9E, 0xF7, 0x11, 0xE3, 0xE1, 0x58, 0x9B, 0xED, 0x34, 0xBB, 0x82, 0xB5, 0xAB, 0x1D,
        0x7D, 0x20, 0x11, 0xDA, 0x33, 0x84, 0x8A, 0x1F, 0x5D, 0xAA, 0x42, 0xE7, 0x8D, 0x32, 0x7C, 0x6F, 0x13, 0xDD, 0xF9, 0x0D, 0xE3, 0x3B, 0x32, 0xB1, 0x6B, 0x6E, 0x7C, 0x9A, 0xB0, 0x43, 0x7B, 0x01,
        0x48, 0x45, 0x41, 0x44, 0x7B, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0xFA, 0x00, 0x00, 0x87, 0x50, 0xF4, 0xC0, 0xA9, 0xC5, 0xA9, 0x66, 0x11, 0x85, 0x0E, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x5B, 0x40, 0x8B, 0x14, 0x5E, 0x27, 0x7E, 0x81, 0xE5, 0xBF, 0x67, 0x7C, 0x94, 0x88, 0x8D, 0x7B, ];

    protected static (string FinalName, Func<Stream, Action<long>, Task> Writer, long Size, string Label)? BuildFileEntry(string entryName, string originalExt, IStorage currentStorage, long size, Nca nca, 
        string label, bool useCompression, bool useBlockMode, int compressionLevel, bool forceKeyGen0, KeySet keySet, Dictionary<string, NcaToNczConverter> converters, CancellationToken ct = default)
    {
        int keyGen = forceKeyGen0 ? 0 : (int)nca.Header.KeyGeneration;

        if (originalExt == ".ncz")
        {
            var ncz = new Ncz(keySet, currentStorage, NczReadMode.Original);
            var decStorage = ncz.BaseStorage;

            decStorage.GetSize(out long decSize).ThrowIfFailure();

            if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
            {
                string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
                var converter = new NcaToNczConverter(keySet);

                converters[Path.ChangeExtension(entryName, ".nca")] = converter;

                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                    using var hs = new MemoryStream(header);

                    await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
                }, decSize, label);
            }
            else
            {
                string finalName = Path.ChangeExtension(entryName, ".nca");
                var cap = decStorage;

                return (finalName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
                }, decSize, label);
            }
        }

        if (useCompression && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
        {
            string finalName = nca.HasSparseLayer() ? entryName : Path.ChangeExtension(entryName, ".ncz");
            var converter = new NcaToNczConverter(keySet);

            converters[Path.ChangeExtension(entryName, ".nca")] = converter;

            var cap = currentStorage;

            return (finalName, async (s, onRead) =>
            {
                var header = await NcaRecryptService.GetRecryptedHeaderAsync(cap, keyGen, keySet, ct);
                using var hs = new MemoryStream(header);

                await converter.ConvertAsync(hs, cap, s, useBlockMode, compressionLevel, onRead, ct);
            }, size, label);
        }
        else
        {
            var cap = currentStorage;

            return (entryName, async (s, onRead) =>
            {
                await NcaRecryptService.RecryptAsync(cap.AsStream(), s, keyGen, keySet, onRead, ct);
            }, size, label);
        }
    }

    protected static async Task RunValidation(Stream fout, Dictionary<string, NcaToNczConverter> converters, long totalValidationSize, string titleId, string displayLabel, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        fout.Position = 0;

        var validationFs = new PartitionFileSystem();

        validationFs.Initialize(fout.AsStorage()).ThrowIfFailure();

        var nczEntries = validationFs.EnumerateEntries("/", "*.ncz")
            .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
            .ToList();

        foreach (var entry in nczEntries)
        {
            ct.ThrowIfCancellationRequested();

            string origName = Path.ChangeExtension(entry.Name, ".nca");

            if (!converters.TryGetValue(origName, out var converter))
                continue;

            using var nczFile = new UniqueRef<IFile>();

            validationFs.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            log?.Invoke($"- {displayLabel} {Res.ToolTip_ValidateCompress}", LogLevel.Info, titleId);

            await converter.ValidateAsync(nczFile.Get.AsStream(), titleId, totalValidationSize, displayLabel, progress, ct);

            log?.Invoke($"- {displayLabel} OK", LogLevel.Ok, titleId);
        }
    }

    protected static Dictionary<string, TitleGroup> BuildTitleGroups(List<MetadataResult> allMeta)
    {
        var groups = new Dictionary<string, TitleGroup>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in allMeta)
        {
            string baseTid = LibHacHelper.GetBaseTitleId(meta.TitleId);

            if (string.IsNullOrEmpty(baseTid))
                continue;

            if (!groups.TryGetValue(baseTid, out var group))
            {
                group = new TitleGroup(baseTid, meta.KrTitle);
                groups[baseTid] = group;
            }

            switch (meta.Type)
            {
                case ContentMetaType.Application: group.BaseMetas.Add(meta); break;
                case ContentMetaType.Patch: group.PatchMetas.Add(meta); break;
                case ContentMetaType.AddOnContent: group.DlcMetas.Add(meta); break;
            }
        }

        return groups;
    }

    protected static (Models.Switch.BuildRequest Request, bool HasBase, bool HasUpdate) BuildSingleRequest(List<MetadataResult> allMeta, IReadOnlyList<string> inputPaths, string outputDir, bool useCompression, int compressionLevel, bool useBlockMode)
    {
        var bases = allMeta.Where(m => m.Type == ContentMetaType.Application).ToList();
        var patches = allMeta.Where(m => m.Type == ContentMetaType.Patch).ToList();
        var dlcs = allMeta.Where(m => m.Type == ContentMetaType.AddOnContent).ToList();
        var baseMeta = bases.FirstOrDefault() ?? patches.OrderByDescending(m => m.TitleVersion).FirstOrDefault() ?? dlcs.FirstOrDefault() ?? allMeta[0];
        string baseTitleId = LibHacHelper.GetBaseTitleId(baseMeta.TitleId);
        var latestPatch = patches
            .Where(m => string.Equals(LibHacHelper.GetBaseTitleId(m.TitleId), baseTitleId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.TitleVersion)
            .FirstOrDefault();
        var versionMeta = latestPatch ?? baseMeta;
        var latestPatches = patches
            .GroupBy(m => LibHacHelper.GetBaseTitleId(m.TitleId), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(m => m.TitleVersion).First())
            .ToList();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in bases.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        foreach (var meta in latestPatches)
            AddNcaIds(allowed, meta);

        foreach (var meta in dlcs.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        var sources = inputPaths
            .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var req = new Models.Switch.BuildRequest(string.Empty, string.Empty, [], outputDir)
        {
            UseCompression = useCompression,
            CompressionLevel = compressionLevel,
            UseBlockMode = useBlockMode,
            AllSourcePaths = sources,
            TargetBaseTitleId = baseTitleId,
            TargetBaseTitleName = baseMeta.KrTitle,
            AllowedNcaIds = allowed.Count > 0 ? allowed : null,
            ResolvedMeta = new MetadataResult(
                baseMeta.TitleId, versionMeta.TitleVersion, versionMeta.DisplayVersion, baseMeta.KrTitle, baseMeta.EnTitle,
                dlcs.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Count(), Type: bases.Count > 0 ? ContentMetaType.Application : baseMeta.Type),
        };

        return (req, bases.Count > 0, patches.Count > 0);
    }

    protected static HashSet<string>? BuildAllowedNcaIds(TitleGroup group, MetadataResult? latestPatch)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var meta in group.BaseMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        if (latestPatch != null)
            AddNcaIds(allowed, latestPatch);

        foreach (var meta in group.DlcMetas.GroupBy(m => m.TitleId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            AddNcaIds(allowed, meta);

        return allowed.Count > 0 ? allowed : null;
    }

    private static void AddNcaIds(HashSet<string> set, MetadataResult meta)
    {
        if (meta.ContentNcaIds == null)
            return;

        foreach (var id in meta.ContentNcaIds) set.Add(id);
    }

    protected static List<string> GetAllPaths(Models.Switch.BuildRequest req)
    {
        if (req.AllSourcePaths is { Count: > 0 })
            return [.. req.AllSourcePaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))];

        var list = new List<string>();

        if (!string.IsNullOrEmpty(req.UpdateFilePath) && File.Exists(req.UpdateFilePath))
            list.Add(req.UpdateFilePath);

        foreach (var p in req.DlcFilePaths)
            if (!string.IsNullOrEmpty(p) && File.Exists(p) && !list.Contains(p))
                list.Add(p);

        if (!string.IsNullOrEmpty(req.BaseFilePath) && !list.Contains(req.BaseFilePath))
            list.Add(req.BaseFilePath);

        return list;
    }

    protected static MetadataResult ExtractFinalMetadata(KeySet ks, List<string> paths, string? targetBaseTitleId = null)
    {
        var allMetas = paths
            .SelectMany(p => MetadataReader.GetMetadataFromContainer(ks, p))
            .GroupBy(m => new { m.TitleId, m.TitleVersion, m.Type })
            .Select(g => g.First())
            .ToList();

        if (!string.IsNullOrEmpty(targetBaseTitleId))
        {
            allMetas = [.. allMetas.Where(m =>
            {
                string baseTid = LibHacHelper.GetBaseTitleId(m.TitleId);

                if (string.IsNullOrEmpty(baseTid))
                    return false;

                return baseTid.Equals(targetBaseTitleId, StringComparison.OrdinalIgnoreCase);
            })];
        }

        if (allMetas.Count == 0)
            return new MetadataResult(string.Empty, 0, "1.0.0", string.Empty, string.Empty, 0, ContentMetaType.Application);

        int dlcCount = allMetas.Count(m => m.Type == ContentMetaType.AddOnContent);
        var latestPatch = allMetas.Where(m => m.Type == ContentMetaType.Patch).OrderByDescending(m => m.TitleVersion).FirstOrDefault();
        var baseGame = allMetas.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? allMetas.First();
        var versionSource = latestPatch ?? baseGame;

        return new MetadataResult(baseGame.TitleId, versionSource.TitleVersion, versionSource.DisplayVersion, baseGame.KrTitle, baseGame.EnTitle, dlcCount, ContentMetaType.Application);
    }

    protected static async Task WriteXciAsync(string displayName, string titleId, byte[] xciPrefixBuffer, List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)> rootEntriesTemplate, Stream? srcStream, 
        List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)> fileEntries, Stream fout, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        await fout.WriteAsync(xciPrefixBuffer, ct);

        long rootHeaderPos = fout.Position;
        var rootBuilderTemp = new Hfs0Builder();

        foreach (var re in rootEntriesTemplate)
            rootBuilderTemp.AddFile(re.Name, 0, new byte[32], 0);

        int rootHeaderSize = (int)rootBuilderTemp.AlignedHeaderSize(MediaSize);

        await fout.WriteAsync(new byte[rootHeaderSize], ct);

        var rootEntryRelOffsets = new Dictionary<string, ulong>();
        var rootEntrySizes = new Dictionary<string, ulong>();
        var rootEntryHashes = new Dictionary<string, byte[]>();
        var rootEntryHashTargetSizes = new Dictionary<string, uint>();
        long rootDataStart = rootHeaderPos + rootHeaderSize;

        foreach (var re in rootEntriesTemplate)
        {
            if (re.Name == "secure")
                continue;

            rootEntryRelOffsets[re.Name] = (ulong)(fout.Position - rootDataStart);
            rootEntrySizes[re.Name] = re.Size;
            rootEntryHashes[re.Name] = re.Hash;
            rootEntryHashTargetSizes[re.Name] = re.HashTargetSize;

            if (srcStream != null)
            {
                srcStream.Position = (long)re.AbsOffset;
                await Utils.CopyStreamAsync(srcStream, fout, (long)re.Size, null, ct);
            }
            else
                await fout.WriteAsync(BuildEmptyHfs0(), ct);
        }

        long secureAbsStart = fout.Position;

        rootEntryRelOffsets["secure"] = (ulong)(secureAbsStart - rootDataStart);

        var secureBuilderTemp = new Hfs0Builder();

        foreach (var (name, _, estimatedSize, _) in fileEntries)
            secureBuilderTemp.AddFile(name, (ulong)estimatedSize, new byte[32], MediaSize);

        int secureHeaderSize = (int)secureBuilderTemp.AlignedHeaderSize(MediaSize);

        await fout.WriteAsync(new byte[secureHeaderSize], ct);

        long secureDataStart = fout.Position;
        long totalEstimated = fileEntries.Sum(f => f.EstimatedSize);
        var reporter = new ProgressReporter(displayName, titleId, totalEstimated, progress);
        void onRead(long bytesRead) => reporter.AddProgress(bytesRead);
        var actualSizes = new ulong[fileEntries.Count];
        var actualHashes = new byte[fileEntries.Count][];
        string currentLabel = string.Empty;
        using var timer = new System.Timers.Timer(200);

        timer.Elapsed += (_, _) => reporter.ForceReport();
        timer.AutoReset = true;

        timer.Start();

        try
        {
            for (int i = 0; i < fileEntries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var (_, writer, _, label) = fileEntries[i];

                currentLabel = label;

                long fileStartPos = fout.Position;
                using var hashStream = new HashTrackingStream(fout, MediaSize);

                await writer(hashStream, onRead);

                actualSizes[i] = (ulong)(fout.Position - fileStartPos);
                actualHashes[i] = hashStream.GetHash();
            }
        }
        finally
        {
            timer.Stop();
        }

        progress?.Report(new ProgressInfo(100, currentLabel, titleId, string.Empty, string.Empty));

        long finalEndPos = fout.Position;
        var secureBuilder = new Hfs0Builder();

        for (int i = 0; i < fileEntries.Count; i++)
            secureBuilder.AddFile(fileEntries[i].Name, actualSizes[i], actualHashes[i], MediaSize);

        byte[] secureHeader = secureBuilder.BuildHeader(MediaSize);
        byte[] secureHash = SHA256.HashData(secureHeader);
        ulong secureTotal = (ulong)(finalEndPos - secureAbsStart);

        fout.Position = secureAbsStart;

        await fout.WriteAsync(secureHeader, ct);

        var rootBuilder = new Hfs0Builder();

        foreach (var re in rootEntriesTemplate)
        {
            string reName = re.Name;
            ulong relOffset = rootEntryRelOffsets[reName];

            if (reName == "secure")
                rootBuilder.AddFileWithOffset("secure", relOffset, secureTotal, secureHash, (uint)secureHeader.Length);
            else
                rootBuilder.AddFileWithOffset(reName, relOffset, rootEntrySizes[reName], rootEntryHashes[reName], rootEntryHashTargetSizes[reName]);
        }

        byte[] rootHeader = rootBuilder.BuildHeader(MediaSize);
        byte[] rootHash = SHA256.HashData(rootHeader);

        fout.Position = rootHeaderPos;

        await fout.WriteAsync(rootHeader, ct);

        BinaryPrimitives.WriteInt64LittleEndian(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderSizePos), rootHeader.Length);
        rootHash.CopyTo(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderHashPos));

        fout.Position = 0;

        await fout.WriteAsync(xciPrefixBuffer, ct);

        fout.Position = finalEndPos;

        await fout.FlushAsync(ct);
    }

    protected static byte[] BuildEmptyHfs0() => new Hfs0Builder().BuildHeader(MediaSize);

    protected static byte[] GetXciPrefix(List<string> allPaths)
    {
        string? xciPath = allPaths.FirstOrDefault(p => Path.GetExtension(p).ToLowerInvariant() is ".xci" or ".xcz");

        if (xciPath != null)
        {
            using var f = File.OpenRead(xciPath);
            using var reader = new BinaryReader(f);

            f.Position = 0x130;

            long hfs0StartOffset = reader.ReadInt64();

            f.Position = 0;

            return reader.ReadBytes((int)hfs0StartOffset);
        }

        byte[] prefix = new byte[0x200];

        XciHeaderTemplate.CopyTo(prefix, 0);
        BinaryPrimitives.WriteInt64LittleEndian(prefix.AsSpan(0x130), 0x200);

        return prefix;
    }

    protected static List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)> GetDummyRootEntries()
    {
        byte[] emptyHfs0 = BuildEmptyHfs0();
        byte[] emptyHash = SHA256.HashData(emptyHfs0);
        ulong emptySize = (ulong)emptyHfs0.Length;

        return [ ("normal", 0, emptySize, emptyHash, (uint)emptyHfs0.Length), ("update", emptySize, emptySize, emptyHash, (uint)emptyHfs0.Length), ("secure", 0, 0, new byte[32], 0), ];
    }

    protected static void CleanupOnFailure(string? finalPath, Action<string, LogLevel, string> log, string titleId)
    {
        if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
            try 
            { 
                File.Delete(finalPath); 
                log?.Invoke(Res.Log_DeleteIncompleteFile, LogLevel.Info, titleId); 
            }
            catch { }
    }
}