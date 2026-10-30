using Common;
using LibHac.Common.Keys;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Utils;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class ContainerConverter
{
    public static Task<string> CompressOrDecompressAsync(OutputContainer output, string inputPath, string? outputDir, bool isCompressMode, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        var profile = ConvertProfile.ForCompression(inputPath, isCompressMode);
        return ConvertAsync(inputPath, outputDir, output, isCompressMode, isCompressMode && validation, isCompressMode && useBlockMode, isCompressMode ? compressionLevel : 0, profile, keySet.Clone(), progress, log, ct);
    }

    public static async Task<string> ConvertAsync(string inputPath, string? outputDir, OutputContainer output, bool useCompression, bool validation, bool useBlockMode, int compressionLevel, ConvertProfile profile, KeySet keySet, 
        IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;

        log?.Invoke(profile.StartLog, LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var source = new SourceSet([inputPath], profile.DropXciUpdatePartition);
            var sourceFs = source.Open(keySet, inputPath, disposables);

            finalPath = Utils.GetUniqueFilePath(Utils.ResolveOutputFilePath(inputPath, output.Extension(useCompression), outputDir));

            string displayName = profile.DisplayNamePrefix + NameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion);
            var fileEntries = new List<FileEntry>();

            foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();

                if (ContainerEntryBuilder.OpenStorage(sourceFs, entry.FullPath, disposables) is not { } opened)
                    continue;

                var (currentStorage, size) = opened;

                if (entryExt is not (".nca" or ".ncz"))
                {
                    fileEntries.Add(ContainerEntryBuilder.CreateCopyEntry(entryName, currentStorage, size, ct));
                    continue;
                }

                var nca = new Nca(keySet, currentStorage);
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";
                var built = ContainerEntryBuilder.BuildNcaEntry(entryName, entryExt, currentStorage, size, nca, label, useCompression, useBlockMode, compressionLevel, false, keySet, converters, profile.CopyStatus, ct);

                if (built.HasValue)
                    fileEntries.Add(built.Value);
            }

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            disposables.Add(fout);
            await output.WriteAsync(source, displayName, meta.TitleId, Pfs0Builder.GetAlignmentPadding(inputPath), fileEntries, fout, progress, disposables, ct);
            await fout.FlushAsync(ct);

            if (profile.LogCompressionRatio)
            {
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = fout.Length;

                log?.Invoke($"{Res.Log_CompressionRatio}: {Common.Utils.FormatFileSize(originalSize)} → {Common.Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight, meta.TitleId);
            }

            if (useCompression && validation && converters.Count > 0)
            {
                var validationFs = output.OpenForValidation(fout, keySet);

                await NczValidator.ValidateAsync(validationFs, converters, fileEntries, meta.TitleId, entryName => profile.ValidationLabel(meta, entryName), profile.ValidationStart, profile.ValidationEnd, progress, log, meta.TitleId, ct);
            }

            isCompleted = true;

            log?.Invoke(profile.DoneLog(finalPath), LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (!isCompleted)
                IncompleteOutput.Delete(finalPath, log, string.Empty);
        }
    }
}