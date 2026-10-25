using Common;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.Core.Utils;
using System.IO;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public class NspRecryptService : BaseSwitchService
{
    public static async Task<List<string>> Recrypt(IReadOnlyList<string> inputPaths, bool forceKeyGen0, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var ks = KeySetProvider.Instance.KeySet.Clone();
        var processedFiles = new List<string>();

        for (int i = 0; i < inputPaths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            string path = inputPaths[i];

            try
            {
                log?.Invoke($"[{i + 1}/{inputPaths.Count}] Recrypt {Res.Log_ProcessStart}: {Path.GetFileName(path)}", LogLevel.Info, path);

                string resultPath = await RunCoreAsync(path, forceKeyGen0, ks.Clone(), progress, log, ct);

                if (!string.IsNullOrEmpty(resultPath))
                    processedFiles.Add(resultPath);
            }
            catch (OperationCanceledException)
            {
                throw; 
            }
            catch (Exception ex)
            {
                log?.Invoke($"{Path.GetFileName(path)} {Res.Log_ProcessError}: {ex.Message}", LogLevel.Error, string.Empty);
            }
        }

        return processedFiles;
    }

    private static async Task<string> RunCoreAsync(string inputPath, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        string? finalPath = null;
        bool isCompleted = false;

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);

            disposables.Add(sourceStorage);

            string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
            string outputExt = inputExt switch
            {
                ".nsz" => ".nsz",
                ".xci" => ".xci",
                ".xcz" => ".xcz",
                _ => ".nsp"
            };

            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, outputExt));

            IFileSystem sourceFs;
            List<(string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize)>? rootEntriesTemplate = null;
            Stream? srcStream = null;
            byte[]? xciPrefixBuffer = null;

            if (inputExt is ".xci" or ".xcz")
            {
                var xci = new Xci(keySet, sourceStorage);
                var rootPartition = xci.OpenPartition(XciPartitionType.Root);

                disposables.Add(rootPartition);

                sourceFs = xci.OpenPartition(XciPartitionType.Secure);

                disposables.Add(sourceFs);

                rootEntriesTemplate = [.. rootPartition
                    .EnumerateEntries("/", "*")
                    .Select(e =>
                    {
                        var (absOffset, size, hash, hashTargetSize) = rootPartition.GetEntryInfo(e.Name.ToString());
                        return (e.Name.ToString(), (ulong)absOffset, (ulong)size, hash, hashTargetSize);
                    })];

                xciPrefixBuffer = GetXciPrefix([inputPath]);
                srcStream = sourceStorage.AsStream();
            }
            else
            {
                sourceFs = sourceStorage.OpenFileSystem(keySet, inputPath);
                disposables.Add(sourceFs);
            }

            keySet.RegisterTickets(sourceFs);

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();

            foreach (var entry in sourceFs.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();
                var fileRef = new UniqueRef<IFile>();

                if (!sourceFs.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
                    continue;

                fileRef.Get.GetSize(out long size).ThrowIfFailure();

                IFile rawFile = fileRef.Release();

                disposables.Add(rawFile);

                IStorage currentStorage = new FileStorage(rawFile);

                disposables.Add(currentStorage);

                if (entryExt is ".tik" or ".cert")
                {
                    if (!forceKeyGen0)
                    {
                        var cap = currentStorage;

                        fileEntries.Add((entryName, async (s, onRead) => await Common.Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));
                    }

                    continue;
                }

                if (entryExt is not ".nca" and not ".ncz")
                {
                    var cap = currentStorage;

                    fileEntries.Add((entryName, async (s, onRead) => await Common.Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));

                    continue;
                }

                var nca = new Nca(keySet, currentStorage);
                int keyGeneration = (int)nca.Header.KeyGeneration;
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";
                var capStorage = currentStorage;

                fileEntries.Add((entryName, async (s, onRead) => await NcaRecryptService.RecryptAsync(capStorage.AsStream(), s, forceKeyGen0 ? 0 : keyGeneration, keySet, onRead, ct), size, $"{label} [Recrypting]"));
            }

            string displayName = $"Recrypt {NameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";

            if (inputExt is ".xci" or ".xcz")
            {
                using var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

                await WriteXciAsync(displayName, meta.TitleId, xciPrefixBuffer!, rootEntriesTemplate!, srcStream, fileEntries, fout, progress, ct);
            }
            else
            {
                var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

                disposables.Add(fout);

                await Pfs0Builder.WriteAsync(displayName, Path.GetFileNameWithoutExtension(finalPath), fileEntries, fout, 0x20, progress, ct);
            }

            isCompleted = true;

            log?.Invoke($"{Path.GetFileName(finalPath)} Recrypt {Res.Log_StatusDone}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        catch (OperationCanceledException)
        { 
            throw;
        }
        catch (Exception ex)
        {
            log?.Invoke($"{Path.GetFileName(inputPath)} {Res.Log_ProcessError}: {ex.Message}", LogLevel.Error, string.Empty);
            throw;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--)
                disposables[i]?.Dispose();

            if (!isCompleted)
                CleanupOnFailure(finalPath, log, string.Empty);
        }
    }
}