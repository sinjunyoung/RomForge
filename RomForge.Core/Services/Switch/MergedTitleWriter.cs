using Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Models;
using NSW.Core.Services;
using NSW.Core.Utils;
using RomForge.Core.Models.Switch;
using System.IO;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class MergedTitleWriter
{
    private const long MetaEstimatedSize = 0x4000;
    private const byte ControlType = 0x3;
    private const byte HtmlDocumentType = 0x4;
    private const byte LegalInformationType = 0x5;
    private const int TitleVersionOffset = 0x8;

    private sealed class CnmtState(byte[] cnmt)
    {
        public byte[] Cnmt { get; set; } = cnmt;
    }

    private sealed class BuiltResult
    {
        public byte[] NcaId { get; set; } = [];
    }

    private sealed record Container(IFileSystem Fs, Dictionary<string, string> Entries);

    private sealed record NcaFile(string Name, IStorage Storage, long Size, Nca Nca);

    private sealed class MergeAssembly(KeySet keySet, int lz4Level, string label, Action<string, LogLevel> log, CancellationToken ct)
    {
        public KeySet KeySet { get; } = keySet;

        public int Lz4Level { get; } = lz4Level;

        public string Label { get; } = label;

        public Action<string, LogLevel> Log { get; } = log;

        public CancellationToken Ct { get; } = ct;

        public List<IDisposable> Disposables { get; } = [];

        public Dictionary<string, NcaToNczConverter> Converters { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<FileEntry> CopyEntries { get; } = [];

        public List<FileEntry> BuildEntries { get; } = [];

        public Dictionary<string, Func<string>> DeferredNames { get; } = new(StringComparer.Ordinal);

        public HashSet<string> AddedNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int PlaceholderIndex { get; set; }
    }

    public static async Task<string> WriteAsync(KeySet keySet, MergePlan plan, string outputDir, int lz4Level, IProgress<ProgressInfo>? progress, Action<string, LogLevel> log, CancellationToken ct)
    {
        var paths = plan.AllPaths;

        if (paths.Any(p => Path.GetExtension(p).ToLowerInvariant() is ".nsz" or ".xcz"))
            throw new NotSupportedException("NSZ/XCZ 입력은 지원하지 않습니다. NSP 또는 XCI를 사용하세요.");

        string label = DisplayName(plan.Base.Meta);
        var asm = new MergeAssembly(keySet, lz4Level, label, log, ct);
        string? finalPath = null;
        bool completed = false;

        try
        {
            var source = new SourceSet(paths);
            var containers = paths.ToDictionary(p => p, p => OpenContainer(source, keySet, p, asm.Disposables), StringComparer.OrdinalIgnoreCase);

            AddBase(asm, plan, containers);

            foreach (var dlc in plan.Dlcs)
                AddDlc(asm, dlc, containers[dlc.Path]);

            asm.CopyEntries.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));

            var entries = new List<FileEntry>(asm.CopyEntries);

            entries.AddRange(asm.BuildEntries);

            var versionSource = plan.Update ?? plan.Base;
            string fileName = NameBuilder.FileNameBuild("Merged_Lz4.nsp", plan.Base.Meta.KrTitle ?? string.Empty, plan.Base.Meta.EnTitle ?? string.Empty, plan.Base.Meta.TitleId.ToUpper(),
                versionSource.Meta.DisplayVersion, versionSource.Meta.TitleVersion, plan.Dlcs.Count);

            Directory.CreateDirectory(outputDir);

            finalPath = Common.Utils.GetUniqueFilePath(Path.Combine(outputDir, fileName));

            var fout = new FileStream(finalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20);

            asm.Disposables.Add(fout);

            string displayName = "Lz4 병합 중 " + NameBuilder.CompressDisplayNameBuild(plan.Base.Meta.KrTitle, plan.Base.Meta.TitleId, versionSource.Meta.DisplayVersion);

            await Pfs0Builder.WriteAsync(displayName, plan.Base.Meta.TitleId, entries, fout, Pfs0Builder.GetAlignmentPadding(plan.Base.Path), true, asm.DeferredNames, progress, ct);
            await fout.FlushAsync(ct);

            long inputSize = paths.Sum(p => new FileInfo(p).Length);

            log($"크기: {Common.Utils.FormatFileSize(inputSize)} → {Common.Utils.FormatFileSize(fout.Length)} ({fout.Length * 100.0 / inputSize:F1}%)", LogLevel.Highlight);

            completed = true;

            return finalPath;
        }
        finally
        {
            for (int i = asm.Disposables.Count - 1; i >= 0; i--)
                asm.Disposables[i]?.Dispose();

            if (!completed)
                IncompleteOutput.Delete(finalPath, (m, l, _) => log(m, l), string.Empty);
        }
    }

    private static void AddBase(MergeAssembly asm, MergePlan plan, Dictionary<string, Container> containers)
    {
        var baseContainer = containers[plan.Base.Path];
        var baseMetaNca = OpenMetaNca(asm, baseContainer, plan.Base.Meta.FileName);
        var state = new CnmtState(Lz4MetaNcaBuilder.ReadCnmt(baseMetaNca));
        var patchRecords = new List<CnmtRecordPatcher.ContentRecord>();
        Container? updateContainer = null;

        if (plan.Update != null)
        {
            updateContainer = containers[plan.Update.Path];

            var patchMetaNca = OpenMetaNca(asm, updateContainer, plan.Update.Meta.FileName);

            patchRecords = CnmtRecordPatcher.ReadRecords(Lz4MetaNcaBuilder.ReadCnmt(patchMetaNca));

            BitConverter.GetBytes(plan.Update.Meta.TitleVersion).CopyTo(state.Cnmt, TitleVersionOffset);
        }

        ulong titleId = BitConverter.ToUInt64(state.Cnmt, 0);
        var baseRecords = CnmtRecordPatcher.ReadRecords(state.Cnmt);

        foreach (var record in baseRecords)
        {
            if (record.Type == CnmtRecordPatcher.ProgramType)
                AddProgram(asm, state, titleId, record, patchRecords, baseContainer, updateContainer);
            else if (record.Type is ControlType or HtmlDocumentType or LegalInformationType)
                AddReplaceable(asm, state, record, patchRecords, baseContainer, updateContainer);
            else
                AddCopy(asm, baseContainer, record);
        }

        if (updateContainer != null)
        {
            var orphans = patchRecords.Where(p => p.Type is ControlType or HtmlDocumentType or LegalInformationType && !baseRecords.Any(b => b.Type == p.Type && b.IdOffset == p.IdOffset)).ToList();

            foreach (var orphan in orphans)
            {
                AddCopy(asm, updateContainer, orphan);

                state.Cnmt = CnmtRecordPatcher.Add(state.Cnmt, orphan);

                asm.Log($"업데이트에만 있는 콘텐츠를 추가했습니다 (Type {orphan.Type}, IdOffset {orphan.IdOffset})", LogLevel.Info);
            }
        }

        AddMeta(asm, state, baseMetaNca, titleId, ContentMetaType.Application);
    }

    private static void AddProgram(MergeAssembly asm, CnmtState state, ulong titleId, CnmtRecordPatcher.ContentRecord record, List<CnmtRecordPatcher.ContentRecord> patchRecords, Container baseContainer, Container? updateContainer)
    {
        var baseFile = OpenNca(asm, baseContainer, record.NcaId);
        var patchIndex = patchRecords.FindIndex(r => r.Type == CnmtRecordPatcher.ProgramType && r.IdOffset == record.IdOffset);
        NcaFile? updateFile = patchIndex >= 0 && updateContainer != null ? OpenNca(asm, updateContainer, patchRecords[patchIndex].NcaId) : null;

        if (updateFile == null && (RomfsCompression.HasCompressedSection(baseFile.Nca) || !baseFile.Nca.CanOpenSection(NcaSectionType.Data)))
        {
            asm.Log($"{baseFile.Name}: 압축할 romfs가 없거나 이미 압축되어 있어 그대로 복사합니다.", LogLevel.Info);
            AddCopy(asm, baseContainer, record);
            return;
        }

        long estimated = MergedProgramNcaBuilder.GetRomfsSize(baseFile.Nca, updateFile?.Nca);
        var built = new BuiltResult();
        string placeholder = NextPlaceholder(asm);

        asm.DeferredNames[placeholder] = () => Convert.ToHexString(built.NcaId).ToLowerInvariant() + ".nca";

        asm.BuildEntries.Add((placeholder, (output, onRead) =>
        {
            asm.Ct.ThrowIfCancellationRequested();
            asm.Log($"Program NCA 병합·Lz4 압축 중 (IdOffset {record.IdOffset})", LogLevel.Highlight);

            var (ncaId, hash, size) = MergedProgramNcaBuilder.BuildInto(output, baseFile.Nca, updateFile?.Nca, titleId, record.IdOffset, asm.KeySet, asm.Lz4Level, onRead, asm.Ct);

            built.NcaId = ncaId;
            state.Cnmt = CnmtRecordPatcher.Replace(state.Cnmt, record.NcaId, ncaId, hash, size);

            return Task.CompletedTask;
        }, estimated, $"{asm.Label} [Program Lz4]"));
    }

    private static void AddReplaceable(MergeAssembly asm, CnmtState state, CnmtRecordPatcher.ContentRecord record, List<CnmtRecordPatcher.ContentRecord> patchRecords, Container baseContainer, Container? updateContainer)
    {
        int patchIndex = patchRecords.FindIndex(r => r.Type == record.Type && r.IdOffset == record.IdOffset);

        if (patchIndex < 0 || updateContainer == null)
        {
            AddCopy(asm, baseContainer, record);
            return;
        }

        var replacement = patchRecords[patchIndex];

        AddCopy(asm, updateContainer, replacement);

        state.Cnmt = CnmtRecordPatcher.Replace(state.Cnmt, record.NcaId, replacement.NcaId, replacement.Hash, replacement.Size);
    }

    private static void AddDlc(MergeAssembly asm, MergeEntry dlc, Container container)
    {
        var metaNca = OpenMetaNca(asm, container, dlc.Meta.FileName);
        var state = new CnmtState(Lz4MetaNcaBuilder.ReadCnmt(metaNca));
        ulong titleId = BitConverter.ToUInt64(state.Cnmt, 0);
        bool replacedAny = false;

        foreach (var record in CnmtRecordPatcher.ReadRecords(state.Cnmt))
        {
            var file = OpenNca(asm, container, record.NcaId);

            if (record.Type != CnmtRecordPatcher.DataType || !file.Nca.CanOpenSection(NcaSectionType.Data) || file.Nca.HasSparseLayer() || RomfsCompression.HasCompressedSection(file.Nca))
            {
                AddCopy(asm, container, record);
                continue;
            }

            replacedAny = true;

            var built = new BuiltResult();
            string placeholder = NextPlaceholder(asm);

            asm.DeferredNames[placeholder] = () => Convert.ToHexString(built.NcaId).ToLowerInvariant() + ".nca";

            asm.BuildEntries.Add((placeholder, (output, onRead) =>
            {
                asm.Ct.ThrowIfCancellationRequested();
                asm.Log($"DLC NCA Lz4 압축 중 ({dlc.Meta.TitleId})", LogLevel.Highlight);

                var (ncaId, hash, size) = Lz4DataNcaBuilder.BuildInto(output, file.Nca, titleId, asm.KeySet, asm.Lz4Level, onRead, asm.Ct);

                built.NcaId = ncaId;
                state.Cnmt = CnmtRecordPatcher.Replace(state.Cnmt, record.NcaId, ncaId, hash, size);

                return Task.CompletedTask;
            }, file.Nca.Header.NcaSize, $"{asm.Label} [DLC Lz4]"));
        }

        if (replacedAny)
            AddMeta(asm, state, metaNca, titleId, ContentMetaType.AddOnContent);
        else
            AddMetaCopy(asm, container, dlc.Meta.FileName);
    }

    private static void AddMeta(MergeAssembly asm, CnmtState state, Nca metaNca, ulong titleId, ContentMetaType type)
    {
        var built = new BuiltResult();
        string placeholder = NextPlaceholder(asm);

        asm.DeferredNames[placeholder] = () => Convert.ToHexString(built.NcaId).ToLowerInvariant() + ".cnmt.nca";

        asm.BuildEntries.Add((placeholder, (output, onRead) =>
        {
            var (ncaId, size) = Lz4MetaNcaBuilder.BuildInto(output, metaNca, state.Cnmt, titleId, type, asm.KeySet, asm.Ct);

            built.NcaId = ncaId;

            onRead(size);

            return Task.CompletedTask;
        }, MetaEstimatedSize, $"{asm.Label} [Meta]"));
    }

    private static void AddMetaCopy(MergeAssembly asm, Container container, string metaFileName)
    {
        if (!container.Entries.TryGetValue(metaFileName, out var path) || ContainerEntryBuilder.OpenStorage(container.Fs, path, asm.Disposables) is not { } opened)
            throw new InvalidOperationException($"Meta NCA를 찾을 수 없습니다: {metaFileName}");

        AddCopyEntry(asm, new NcaFile(metaFileName, opened.Storage, opened.Size, new Nca(asm.KeySet, opened.Storage)));
    }

    private static void AddCopy(MergeAssembly asm, Container container, CnmtRecordPatcher.ContentRecord record) => AddCopyEntry(asm, OpenNca(asm, container, record.NcaId));

    private static void AddCopyEntry(MergeAssembly asm, NcaFile file)
    {
        if (!asm.AddedNames.Add(file.Name))
            return;

        var entry = ContainerEntryBuilder.BuildNcaEntry(file.Name, ".nca", file.Storage, file.Size, file.Nca, $"{asm.Label} [{file.Nca.Header.ContentType}]", false, false, 0, false, asm.KeySet, asm.Converters, null, asm.Ct);

        if (entry.HasValue)
            asm.CopyEntries.Add(entry.Value);
    }

    private static Container OpenContainer(SourceSet source, KeySet keySet, string path, List<IDisposable> disposables)
    {
        var fs = source.Open(keySet, path, disposables);
        var entries = fs.EnumerateEntries("/", "*").ToDictionary(e => e.Name.ToString(), e => e.FullPath, StringComparer.OrdinalIgnoreCase);
        return new Container(fs, entries);
    }

    private static Nca OpenMetaNca(MergeAssembly asm, Container container, string metaFileName)
    {
        if (!container.Entries.TryGetValue(metaFileName, out var path) || ContainerEntryBuilder.OpenStorage(container.Fs, path, asm.Disposables) is not { } opened)
            throw new InvalidOperationException($"Meta NCA를 찾을 수 없습니다: {metaFileName}");

        return new Nca(asm.KeySet, opened.Storage);
    }

    private static NcaFile OpenNca(MergeAssembly asm, Container container, byte[] ncaId)
    {
        string name = Convert.ToHexString(ncaId).ToLowerInvariant() + ".nca";

        if (!container.Entries.TryGetValue(name, out var path) || ContainerEntryBuilder.OpenStorage(container.Fs, path, asm.Disposables) is not { } opened)
            throw new InvalidOperationException($"NCA 파일을 찾을 수 없습니다: {name}");

        return new NcaFile(name, opened.Storage, opened.Size, new Nca(asm.KeySet, opened.Storage));
    }

    private static string NextPlaceholder(MergeAssembly asm) => "$" + (asm.PlaceholderIndex++).ToString("x31") + ".nca";

    private static string DisplayName(MetadataResult meta) => !string.IsNullOrWhiteSpace(meta.KrTitle) ? meta.KrTitle : !string.IsNullOrWhiteSpace(meta.EnTitle) ? meta.EnTitle : meta.TitleId;
}