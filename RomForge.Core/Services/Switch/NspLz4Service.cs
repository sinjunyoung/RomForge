using Common;
using LibHac.Common.Keys;
using LibHac.Ncm;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Models;
using NSW.Core.Services;
using NSW.Core.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

public static class NspLz4Service
{
    private const long MetaEstimatedSize = 0x4000;

    private sealed record Target(CnmtRecordPatcher.ContentRecord Record, string EntryName, Nca Nca);

    private sealed class MetaJob(MetadataResult meta, Nca metaNca, byte[] cnmt, ulong titleId)
    {
        public MetadataResult Meta { get; } = meta;

        public Nca MetaNca { get; } = metaNca;

        public byte[] Cnmt { get; set; } = cnmt;

        public ulong TitleId { get; } = titleId;

        public List<Target> Targets { get; } = [];
    }

    private sealed class BuiltResult
    {
        public byte[] NcaId { get; set; } = [];

        public byte[] Hash { get; set; } = [];

        public long Size { get; set; }
    }

    public static Task<string> CompressAsync(string inputPath, string? outputDir, int lz4Level, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return ConvertAsync(keySet.Clone(), inputPath, outputDir, lz4Level, progress, log, ct);
    }

    internal static async Task<string> ConvertAsync(KeySet keySet, string inputPath, string? outputDir, int lz4Level, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        if (!string.Equals(Path.GetExtension(inputPath), ".nsp", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("NSP 파일만 지원합니다.");

        var disposables = new List<IDisposable>();
        string? finalPath = null;
        bool isCompleted = false;

        try
        {
            var allMetas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);
            bool hasPatch = allMetas.Any(m => m.Type == ContentMetaType.Patch);
            var metas = allMetas.Where(m => m.Type is ContentMetaType.Application or ContentMetaType.AddOnContent).ToList();

            if (metas.Count == 0)
                throw new InvalidOperationException("Application(본편) 또는 DLC 메타데이터를 찾을 수 없습니다. 업데이트 NSP는 지원하지 않습니다.");

            var primary = metas.FirstOrDefault(m => m.Type == ContentMetaType.Application) ?? metas[0];

            log?.Invoke($"{Path.GetFileName(inputPath)} Lz4 압축 시작", LogLevel.Info, primary.TitleId);

            var source = new SourceSet([inputPath]);
            var sourceFs = source.Open(keySet, inputPath, disposables);
            var entryPaths = sourceFs.EnumerateEntries("/", "*").ToDictionary(e => e.Name.ToString(), e => e.FullPath, StringComparer.OrdinalIgnoreCase);
            var jobs = new List<MetaJob>();

            foreach (var meta in metas)
            {
                if (meta.Type == ContentMetaType.Application && hasPatch)
                {
                    log?.Invoke("업데이트가 함께 들어 있는 본편은 업데이트 패치가 깨질 수 있어 건너뜁니다.", LogLevel.Warning, meta.TitleId);
                    continue;
                }

                var job = LoadJob(keySet, meta, entryPaths, sourceFs, disposables, log);

                if (job.Targets.Count > 0)
                    jobs.Add(job);
            }

            if (jobs.Count == 0)
                throw new InvalidOperationException("Lz4로 압축할 romfs가 없거나 이미 모두 압축되어 있습니다.");

            string directory = string.IsNullOrWhiteSpace(outputDir) ? Path.GetDirectoryName(inputPath)! : outputDir;

            Directory.CreateDirectory(directory);

            finalPath = Utils.GetUniqueFilePath(Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + "_lz4.nsp"));

            string label = primary.KrTitle ?? primary.EnTitle;
            var replaced = new HashSet<string>(jobs.SelectMany(j => j.Targets.Select(t => t.EntryName).Append(j.Meta.FileName)), StringComparer.OrdinalIgnoreCase);
            var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
            var fileEntries = new List<FileEntry>();

            foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();

                if (replaced.Contains(entryName))
                    continue;

                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();

                if (ContainerEntryBuilder.OpenStorage(sourceFs, entry.FullPath, disposables) is not { } opened)
                    continue;

                var (storage, size) = opened;

                if (entryExt is not (".nca" or ".ncz"))
                {
                    fileEntries.Add(ContainerEntryBuilder.CreateCopyEntry(entryName, storage, size, ct));
                    continue;
                }

                var nca = new Nca(keySet, storage);
                var copied = ContainerEntryBuilder.BuildNcaEntry(entryName, entryExt, storage, size, nca, $"{label} [{nca.Header.ContentType}]", false, false, 0, false, keySet, converters, null, ct);

                if (copied.HasValue)
                    fileEntries.Add(copied.Value);
            }

            fileEntries.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));

            var deferredNames = new Dictionary<string, Func<string>>(StringComparer.Ordinal);
            int placeholderIndex = 0;

            foreach (var job in jobs)
                AddJobEntries(job, keySet, lz4Level, label, ct, log, fileEntries, deferredNames, ref placeholderIndex);

            var fout = new FileStream(finalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20);

            disposables.Add(fout);

            string displayName = "Lz4 압축 중 " + NameBuilder.CompressDisplayNameBuild(primary.KrTitle, primary.TitleId, primary.DisplayVersion);

            await Pfs0Builder.WriteAsync(displayName, primary.TitleId, fileEntries, fout, Pfs0Builder.GetAlignmentPadding(inputPath), true, deferredNames, progress, ct);
            await fout.FlushAsync(ct);

            long originalSize = new FileInfo(inputPath).Length;

            log?.Invoke($"{Res.Log_CompressionRatio}: {Common.Utils.FormatFileSize(originalSize)} → {Common.Utils.FormatFileSize(fout.Length)} ({fout.Length * 100.0 / originalSize:F1}%)", LogLevel.Highlight, primary.TitleId);

            isCompleted = true;

            log?.Invoke($"Lz4 압축 완료: {Path.GetFileName(finalPath)}", LogLevel.Ok, primary.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (!isCompleted)
                IncompleteOutput.Delete(finalPath, log!, string.Empty);
        }
    }

    private static MetaJob LoadJob(KeySet keySet, MetadataResult meta, Dictionary<string, string> entryPaths, LibHac.Fs.Fsa.IFileSystem sourceFs, List<IDisposable> disposables, Action<string, LogLevel, string>? log)
    {
        if (!entryPaths.TryGetValue(meta.FileName, out var metaEntryPath) || ContainerEntryBuilder.OpenStorage(sourceFs, metaEntryPath, disposables) is not { } metaOpened)
            throw new InvalidOperationException($"Meta NCA를 찾을 수 없습니다: {meta.FileName}");

        var metaNca = new Nca(keySet, metaOpened.Storage);
        byte[] cnmt = Lz4MetaNcaBuilder.ReadCnmt(metaNca);
        var job = new MetaJob(meta, metaNca, cnmt, BitConverter.ToUInt64(cnmt, 0));
        byte recordType = meta.Type == ContentMetaType.Application ? CnmtRecordPatcher.ProgramType : CnmtRecordPatcher.DataType;

        foreach (var record in CnmtRecordPatcher.ReadRecords(cnmt).Where(r => r.Type == recordType))
        {
            string entryName = $"{Convert.ToHexString(record.NcaId).ToLowerInvariant()}.nca";

            if (!entryPaths.TryGetValue(entryName, out var entryPath) || ContainerEntryBuilder.OpenStorage(sourceFs, entryPath, disposables) is not { } opened)
            {
                log?.Invoke($"{entryName}: NCA 파일을 찾을 수 없어 건너뜁니다.", LogLevel.Warning, meta.TitleId);
                continue;
            }

            var nca = new Nca(keySet, opened.Storage);

            if (!nca.CanOpenSection(NcaSectionType.Data))
            {
                log?.Invoke($"{entryName}: romfs 섹션이 없어 건너뜁니다.", LogLevel.Warning, meta.TitleId);
                continue;
            }

            if (nca.HasSparseLayer())
            {
                log?.Invoke($"{entryName}: Sparse 레이어가 있는 NCA는 지원하지 않아 건너뜁니다.", LogLevel.Warning, meta.TitleId);
                continue;
            }

            if (RomfsCompression.HasCompressedSection(nca))
            {
                log?.Invoke($"{entryName}: 이미 압축되어 있어 건너뜁니다.", LogLevel.Info, meta.TitleId);
                continue;
            }

            job.Targets.Add(new Target(record, entryName, nca));
        }

        return job;
    }

    private static void AddJobEntries(MetaJob job, KeySet keySet, int lz4Level, string label, CancellationToken ct, Action<string, LogLevel, string>? log, List<FileEntry> fileEntries, Dictionary<string, Func<string>> deferredNames, ref int placeholderIndex)
    {
        bool isApplication = job.Meta.Type == ContentMetaType.Application;
        string kind = isApplication ? "Program" : "DLC";

        for (int i = 0; i < job.Targets.Count; i++)
        {
            int number = i + 1;
            int total = job.Targets.Count;
            var target = job.Targets[i];
            var built = new BuiltResult();
            string placeholder = PlaceholderName(placeholderIndex++);

            deferredNames[placeholder] = () => Convert.ToHexString(built.NcaId).ToLowerInvariant() + ".nca";

            fileEntries.Add((placeholder, (output, onRead) =>
            {
                ct.ThrowIfCancellationRequested();

                log?.Invoke($"{kind} NCA Lz4 압축 중 ({number}/{total}, {job.Meta.TitleId})", LogLevel.Highlight, job.Meta.TitleId);

                var result = isApplication
                    ? Lz4ProgramNcaBuilder.BuildInto(output, target.Nca, job.TitleId, target.Record.IdOffset, keySet, lz4Level, onRead, ct)
                    : Lz4DataNcaBuilder.BuildInto(output, target.Nca, job.TitleId, keySet, lz4Level, onRead, ct);

                built.NcaId = result.NcaId;
                built.Hash = result.Hash;
                built.Size = result.Size;
                job.Cnmt = CnmtRecordPatcher.Replace(job.Cnmt, target.Record.NcaId, result.NcaId, result.Hash, result.Size);

                return Task.CompletedTask;
            }, (long)target.Nca.Header.NcaSize, $"{label} [{kind} Lz4]"));
        }

        var metaBuilt = new BuiltResult();
        string metaPlaceholder = PlaceholderName(placeholderIndex++);

        deferredNames[metaPlaceholder] = () => Convert.ToHexString(metaBuilt.NcaId).ToLowerInvariant() + ".cnmt.nca";

        fileEntries.Add((metaPlaceholder, (output, onRead) =>
        {
            var result = Lz4MetaNcaBuilder.BuildInto(output, job.MetaNca, job.Cnmt, job.TitleId, job.Meta.Type, keySet, ct);

            metaBuilt.NcaId = result.NcaId;
            metaBuilt.Size = result.Size;

            onRead(result.Size);

            return Task.CompletedTask;
        }, MetaEstimatedSize, $"{label} [Meta]"));
    }

    private static string PlaceholderName(int index) => "$" + index.ToString("x31") + ".nca";
}