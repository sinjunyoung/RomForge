using Common;
using LibHac.Common.Keys;
using LibHac.NSZ;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Services;
using NSW.Core.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

public static class NspLz4Service
{
    private const int BuildPercent = 80;

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
        string? workDir = null;
        bool isCompleted = false;

        try
        {
            var meta = MetadataReader.GetMetadataFromContainer(keySet, inputPath).FirstOrDefault(m => m.Type == LibHac.Ncm.ContentMetaType.Application)
                ?? throw new InvalidOperationException("Application(본편) 메타데이터를 찾을 수 없습니다. 업데이트/DLC NSP는 지원하지 않습니다.");

            log?.Invoke($"{Path.GetFileName(inputPath)} Lz4 압축 시작", LogLevel.Info, meta.TitleId);

            var source = new SourceSet([inputPath]);
            var sourceFs = source.Open(keySet, inputPath, disposables);
            var entryPaths = sourceFs.EnumerateEntries("/", "*").ToDictionary(e => e.Name.ToString(), e => e.FullPath, StringComparer.OrdinalIgnoreCase);

            if (!entryPaths.TryGetValue(meta.FileName, out var metaEntryPath) || ContainerEntryBuilder.OpenStorage(sourceFs, metaEntryPath, disposables) is not { } metaOpened)
                throw new InvalidOperationException($"Meta NCA를 찾을 수 없습니다: {meta.FileName}");

            var metaNca = new Nca(keySet, metaOpened.Storage);
            byte[] cnmt = Lz4MetaNcaBuilder.ReadCnmt(metaNca);
            ulong baseTitleId = BitConverter.ToUInt64(cnmt, 0);
            var targets = new List<(CnmtRecordPatcher.ContentRecord Record, string EntryName, Nca Nca)>();

            foreach (var record in CnmtRecordPatcher.ReadRecords(cnmt).Where(r => r.Type == CnmtRecordPatcher.ProgramType))
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

                targets.Add((record, entryName, nca));
            }

            if (targets.Count == 0)
                throw new InvalidOperationException("Lz4로 압축할 romfs가 없거나 이미 모두 압축되어 있습니다.");

            string directory = string.IsNullOrWhiteSpace(outputDir) ? Path.GetDirectoryName(inputPath)! : outputDir;

            Directory.CreateDirectory(directory);

            finalPath = Utils.GetUniqueFilePath(Path.Combine(directory, Path.GetFileNameWithoutExtension(inputPath) + "_lz4.nsp"));
            workDir = finalPath + ".work";

            Directory.CreateDirectory(workDir);

            string label = meta.KrTitle ?? meta.EnTitle;
            var builtNcas = new List<(BuiltNca Nca, string Label)>();

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var target = targets[i];

                log?.Invoke($"Program NCA Lz4 압축 중 ({i + 1}/{targets.Count}, IdOffset={target.Record.IdOffset})", LogLevel.Highlight, meta.TitleId);

                var scaled = new ScaledProgress(progress, BuildPercent * i / targets.Count, BuildPercent * (i + 1) / targets.Count);
                var built = Lz4ProgramNcaBuilder.Build(target.Nca, baseTitleId, target.Record.IdOffset, keySet, workDir, lz4Level, scaled, ct);

                cnmt = CnmtRecordPatcher.Replace(cnmt, target.Record.NcaId, built.NcaId, built.Hash, built.Size);

                builtNcas.Add((built, $"{label} [Program Lz4]"));
            }

            builtNcas.Add((Lz4MetaNcaBuilder.Build(metaNca, cnmt, baseTitleId, keySet, workDir, ct), $"{label} [Meta]"));

            var replaced = new HashSet<string>(targets.Select(t => t.EntryName).Append(meta.FileName), StringComparer.OrdinalIgnoreCase);
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

            foreach (var (built, entryLabel) in builtNcas)
                fileEntries.Add(CreateFileEntry(built, entryLabel, ct));

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            disposables.Add(fout);

            string displayName = "Lz4 압축 중 " + NameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion);

            await Pfs0Builder.WriteAsync(displayName, meta.TitleId, fileEntries, fout, Pfs0Builder.GetAlignmentPadding(inputPath), new ScaledProgress(progress, BuildPercent, 100), ct);
            await fout.FlushAsync(ct);

            long originalSize = new FileInfo(inputPath).Length;

            log?.Invoke($"{Res.Log_CompressionRatio}: {Common.Utils.FormatFileSize(originalSize)} → {Common.Utils.FormatFileSize(fout.Length)} ({fout.Length * 100.0 / originalSize:F1}%)", LogLevel.Highlight, meta.TitleId);

            isCompleted = true;

            log?.Invoke($"Lz4 압축 완료: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (workDir != null && Directory.Exists(workDir))
                try { Directory.Delete(workDir, true); } catch { }

            if (!isCompleted)
                IncompleteOutput.Delete(finalPath, log!, string.Empty);
        }
    }

    private static FileEntry CreateFileEntry(BuiltNca built, string label, CancellationToken ct) => (built.FileName, async (output, onRead) =>
    {
        using var input = new FileStream(built.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);

        await Utils.CopyStreamAsync(input, output, onRead, ct);
    }, built.Size, label);
}