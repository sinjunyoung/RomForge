using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Utils;
using RomForge.Core.Models.Switch;
using RomZip.Core.Services;
using System.IO;

using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public class NspXciConvertService : BaseSwitchService
{
    public static Task<string> NspToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NspToXczAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNszAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXczAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNszAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    private static async Task<string> RunAsync(string inputPath, ContainerFormat outputFormat, bool useCompression, bool validation, bool useBlockMode, int compressionLevel, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;
        string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
        bool inputIsXci = inputExt is ".xci" or ".xcz";
        string outputExt = outputFormat == ContainerFormat.Xci ? (useCompression ? ".xcz" : ".xci") : (useCompression ? ".nsz" : ".nsp");

        log?.Invoke($"{Path.GetFileName(inputPath)} → {outputExt.TrimStart('.').ToUpper()} 변환 시작", LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);

            disposables.Add(sourceStorage);

            IFileSystem secureFs;

            if (inputIsXci)
            {
                var xci = new Xci(keySet, sourceStorage);

                secureFs = xci.OpenPartition(XciPartitionType.Secure);
                disposables.Add(secureFs);
            }
            else
            {
                secureFs = sourceStorage.OpenFileSystem(keySet, inputPath);
                disposables.Add(secureFs);
            }

            keySet.RegisterTickets(secureFs);

            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, outputExt));

            string displayName = NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion);
            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();

            foreach (var entry in secureFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();
                var fileRef = new UniqueRef<IFile>();

                if (!secureFs.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                    continue;

                fileRef.Get.GetSize(out long size).ThrowIfFailure();

                IFile rawFile = fileRef.Release();

                disposables.Add(rawFile);

                IStorage currentStorage = new FileStorage(rawFile);

                disposables.Add(currentStorage);

                if (entryExt is ".tik" or ".cert")
                {
                    var cap = currentStorage;
                    fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));
                    continue;
                }

                if (entryExt is not ".nca" and not ".ncz")
                {
                    var cap = currentStorage;
                    fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));
                    continue;
                }

                var nca = new Nca(keySet, currentStorage);
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";
                var built = BuildFileEntry(entryName, entryExt, currentStorage, size, nca, label, useCompression, useBlockMode, compressionLevel, false, keySet, converters, ct);

                if (built.HasValue)
                    fileEntries.Add(built.Value);
            }

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            disposables.Add(fout);

            if (outputFormat == ContainerFormat.Xci)
            {
                byte[] xciPrefix = GetXciPrefix(inputIsXci ? [inputPath] : []);
                var rootEntries = inputIsXci ? GetRootEntriesFromXci(keySet, sourceStorage) : GetDummyRootEntries();

                await WriteXciAsync(displayName, meta.TitleId, xciPrefix, rootEntries, inputIsXci ? sourceStorage.AsStream() : null, fileEntries, fout, progress, ct);

                if (useCompression && validation && converters.Count > 0)
                    await RunValidation(fout, converters, fileEntries.Sum(f => f.EstimatedSize), meta.TitleId, displayName, progress, log, ct);
            }
            else
            {
                await Pfs0Builder.WriteAsync(displayName, meta.TitleId, fileEntries, fout, Pfs0Builder.GetAlignmentPadding(inputPath), progress, ct);

                if (useCompression && validation && converters.Count > 0)
                    await RunValidation(fout, converters, fileEntries.Sum(f => f.EstimatedSize), meta.TitleId, displayName, progress, log, ct);

                await fout.FlushAsync(ct);
            }

            isCompleted = true;
            log?.Invoke($"변환 완료: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (!isCompleted) 
                CleanupOnFailure(finalPath, log, string.Empty);
        }
    }

    private static List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)> GetRootEntriesFromXci(KeySet keySet, LocalStorage sourceStorage)
    {
        var xci = new Xci(keySet, sourceStorage);
        var rootPartition = xci.OpenPartition(XciPartitionType.Root);

        return [.. rootPartition
            .EnumerateEntries("/", "*")
            .Select(e =>
            {
                var (absOffset, size, hash, hashTargetSize) = rootPartition.GetEntryInfo(e.Name.ToString());

                return (e.Name.ToString(), (ulong)absOffset, (ulong)size, hash, hashTargetSize);
            })];
    }
}