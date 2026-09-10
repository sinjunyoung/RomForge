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
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public static class XciCompressService
{
    private const long XciHfs0HeaderSizePos = 0x138;
    private const long XciHfs0HeaderHashPos = 0x140;
    private const ulong MediaSize = 0x200;
    private const int CopyBufferSize = 1024 * 1024;

    public static Task<string> CompressAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, true, compressionLevel, validation, useBlockMode, false, keySet?.Clone(), progress, log, ct);
    }

    public static Task<string> DecompressAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

        return RunAsync(inputPath, false, 0, false, false, false, keySet?.Clone(), progress, log, ct);
    }

    private static async Task<string> RunAsync(string inputPath, bool isCompressMode, int compressionLevel, bool validation, bool useBlockMode, bool forceKeyGen0, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        var converters = new Dictionary<string, NcaToNczConverter>(StringComparer.OrdinalIgnoreCase);
        string? finalPath = null;
        bool isCompleted = false;
        string modeDone = isCompressMode ? Res.Log_ModeCompress : Res.Log_ModeDecompress;

        log?.Invoke($"{Path.GetFileName(inputPath)} {modeDone} {Res.Log_ProcessStart}", LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);

            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            long hfs0StartOffset;
            byte[] xciPrefixBuffer;
            using (var xciHeaderFile = File.OpenRead(inputPath))
            using (var reader = new BinaryReader(xciHeaderFile))
            {
                xciHeaderFile.Position = 0x130;
                hfs0StartOffset = reader.ReadInt64();
                xciHeaderFile.Position = 0;
                xciPrefixBuffer = reader.ReadBytes((int)hfs0StartOffset);
            }

            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);

            disposables.Add(sourceStorage);

            var xci = new Xci(keySet, sourceStorage);
            var rootPartition = xci.OpenPartition(XciPartitionType.Root);

            disposables.Add(rootPartition);

            var securePartition = xci.OpenPartition(XciPartitionType.Secure);

            disposables.Add(securePartition);
            keySet.RegisterTickets(securePartition);

            string outputExt = isCompressMode ? ".xcz" : ".xci";

            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, outputExt));

            string displayName = $"{(isCompressMode ? Res.Log_StatusCompressing : Res.Log_StatusDecompressing)} {NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";
            var rootEntries = rootPartition.EnumerateEntries("/", "*").ToList();

            if (isCompressMode)
                rootEntries.RemoveAll(re => string.Equals(re.Name.ToString(), "update", StringComparison.OrdinalIgnoreCase));

            var fileEntries = new List<(string Name, Func<Stream, Action<long>, Task> Writer, long EstimatedSize, string Label)>();

            foreach (var entry in securePartition.EnumerateEntries("/", "*"))
            {
                string entryName = entry.Name.ToString();
                string entryExt = Path.GetExtension(entryName).ToLowerInvariant();
                var fileRef = new UniqueRef<IFile>();

                if (!securePartition.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).IsSuccess())
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

                        fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));
                    }
                    continue;
                }

                if (entryExt is not ".nca" and not ".ncz")
                {
                    var cap = currentStorage;

                    fileEntries.Add((entryName, async (s, onRead) => await Utils.CopyStreamAsync(cap.AsStream(), s, onRead, ct), size, entryName));

                    continue;
                }

                IStorage ncaStorage;
                string ncaName;
                long ncaSize;

                if (entryExt == ".ncz")
                {
                    var ncz = new Ncz(keySet, currentStorage.AsStream(), NczReadMode.Original);

                    ncz.BaseStorage.GetSize(out ncaSize).ThrowIfFailure();

                    ncaStorage = ncz.BaseStorage;
                    ncaName = Path.ChangeExtension(entryName, ".nca");
                }
                else
                {
                    ncaStorage = currentStorage;
                    ncaName = entryName;
                    ncaSize = size;
                }

                var nca = new Nca(keySet, ncaStorage);
                string label = $"{meta.KrTitle ?? meta.EnTitle} [{nca.Header.ContentType}]";

                if (isCompressMode && nca.Header.ContentType is NcaContentType.Program or NcaContentType.PublicData)
                {
                    string compName = nca.HasSparseLayer() ? ncaName : Path.ChangeExtension(ncaName, ".ncz");
                    var converter = new NcaToNczConverter(keySet);

                    converters[ncaName] = converter;

                    var capStorage = ncaStorage;

                    fileEntries.Add((compName, async (s, onRead) =>
                    {
                        var recryptedHeader = await NcaRecryptService.GetRecryptedHeaderAsync(capStorage, nca.Header.KeyGeneration, keySet, ct);
                        using var headerStream = new MemoryStream(recryptedHeader);

                        await converter.ConvertAsync(headerStream, capStorage, s, useBlockMode, compressionLevel, onRead, ct);
                    }, size, label));
                }
                else
                {
                    var capStorage = ncaStorage;
                    string statusLabel = isCompressMode ? Res.Log_StatusCopying : Res.Log_StatusDecompressing;

                    fileEntries.Add((ncaName, async (s, onRead) =>
                    {
                        await NcaRecryptService.RecryptAsync(capStorage.AsStream(), s, nca.Header.KeyGeneration, keySet, onRead, ct);
                    }, ncaSize, $"{label} [{statusLabel}]"));
                }
            }

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);

            disposables.Add(fout);

            await fout.WriteAsync(xciPrefixBuffer, ct);

            long rootHeaderPos = fout.Position;
            var rootBuilderTemp = new Hfs0Builder();

            foreach (var re in rootEntries)
                rootBuilderTemp.AddFile(re.Name.ToString(), 0, new byte[32], 0);

            int rootHeaderSize = (int)rootBuilderTemp.AlignedHeaderSize(MediaSize);

            await fout.WriteAsync(new byte[rootHeaderSize], ct);

            var rootEntryRelOffsets = new Dictionary<string, ulong>();
            var rootEntrySizes = new Dictionary<string, ulong>();
            var rootEntryHashes = new Dictionary<string, byte[]>();
            var rootEntryHashTargetSizes = new Dictionary<string, uint>();
            long rootDataStart = rootHeaderPos + rootHeaderSize;
            var srcStream = sourceStorage.AsStream();

            foreach (var re in rootEntries)
            {
                string reName = re.Name.ToString();

                if (reName == "secure")
                    continue;

                var (absOffset, reSize, reHash, reHashTargetSize) = rootPartition.GetEntryInfo(reName);

                rootEntryRelOffsets[reName] = (ulong)(fout.Position - rootDataStart);
                rootEntrySizes[reName] = (ulong)reSize;
                rootEntryHashes[reName] = reHash;
                rootEntryHashTargetSizes[reName] = reHashTargetSize;
                srcStream.Position = absOffset;

                await Utils.CopyStreamAsync(srcStream, fout, reSize, null, ct);
            }

            long secureAbsStart = fout.Position;

            rootEntryRelOffsets["secure"] = (ulong)(secureAbsStart - rootDataStart);

            var secureBuilderTemp = new Hfs0Builder();

            foreach (var (name, _, estimatedSize, _) in fileEntries)
                secureBuilderTemp.AddFile(name, (ulong)estimatedSize, new byte[32], 0x200);

            int secureHeaderSize = (int)secureBuilderTemp.AlignedHeaderSize(MediaSize);

            await fout.WriteAsync(new byte[secureHeaderSize], ct);

            long secureDataStart = fout.Position;
            long totalEstimated = fileEntries.Sum(f => f.EstimatedSize);
            string currentLabel = string.Empty;
            var reporter = new ProgressReporter(displayName, meta.TitleId, totalEstimated, progress);

            void onRead(long bytesRead) => reporter.AddProgress(bytesRead);

            var actualOffsets = new ulong[fileEntries.Count];
            var actualSizes = new ulong[fileEntries.Count];
            var actualHashes = new byte[fileEntries.Count][];
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
                    actualOffsets[i] = (ulong)(fout.Position - secureDataStart);

                    long fileStartPos = fout.Position;
                    using var hashStream = new HashTrackingStream(fout, 0x200);

                    await writer(hashStream, onRead);

                    actualSizes[i] = (ulong)(fout.Position - fileStartPos);
                    actualHashes[i] = hashStream.GetHash();
                }
            }
            finally
            {
                timer.Stop();
            }

            progress?.Report(new ProgressInfo(100, currentLabel, meta.TitleId, string.Empty, string.Empty));

            long finalEndPos = fout.Position;
            var secureBuilder = new Hfs0Builder();

            for (int i = 0; i < fileEntries.Count; i++)
                secureBuilder.AddFile(fileEntries[i].Name, actualSizes[i], actualHashes[i], 0x200);

            byte[] secureHeader = secureBuilder.BuildHeader(MediaSize);
            byte[] secureHash = SHA256.HashData(secureHeader);
            ulong secureTotal = (ulong)(finalEndPos - secureAbsStart);

            fout.Position = secureAbsStart;

            await fout.WriteAsync(secureHeader, ct);

            var rootBuilder = new Hfs0Builder();

            foreach (var re in rootEntries)
            {
                string reName = re.Name.ToString();
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

            if (isCompressMode)
            {
                long originalSize = new FileInfo(inputPath).Length;
                long compressedSize = fout.Length;

                log?.Invoke($"{Res.Log_CompressionRatio}: {Utils.FormatFileSize(originalSize)} → {Utils.FormatFileSize(compressedSize)} ({compressedSize * 100.0 / originalSize:F1}%)", LogLevel.Highlight, meta.TitleId);

                if (validation)
                {
                    fout.Position = 0;

                    var validationStorage = new StreamStorage(fout, false);
                    var validationXci = new Xci(keySet, validationStorage);
                    var validationSecure = validationXci.OpenPartition(XciPartitionType.Secure);
                    var nczEntries = validationSecure.EnumerateEntries("/", "*.ncz")
                        .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
                        .ToList();
                    long totalValidationSize = nczEntries.Sum(e => fileEntries.FirstOrDefault(f => string.Equals(f.Name, e.Name, StringComparison.OrdinalIgnoreCase)).EstimatedSize);

                    foreach (var entry in nczEntries)
                    {
                        ct.ThrowIfCancellationRequested();

                        string origName = Path.ChangeExtension(entry.Name, ".nca");

                        if (!converters.TryGetValue(origName, out var converter))
                            continue;

                        using var nczFile = new UniqueRef<IFile>();

                        validationSecure.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        string label = $"{meta.KrTitle ?? meta.EnTitle} [{meta.TitleId}]";

                        log?.Invoke($"- {label} {Res.Log_StatusValidating}", LogLevel.Info, meta.TitleId);

                        await converter.ValidateAsync(nczFile.Get.AsStream(), Path.GetFileNameWithoutExtension(finalPath), totalValidationSize, label, progress, ct);

                        log?.Invoke($"- {label} {Res.Log_ValidationComplete}", LogLevel.Ok, meta.TitleId);
                    }
                }
            }

            isCompleted = true;

            log?.Invoke($"{modeDone} {Res.Log_StatusDone}: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();

            if (!isCompleted && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                try
                {
                    File.Delete(finalPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"실패한 결과 파일 삭제 실패 ({finalPath}): {ex.Message}");
                }
        }
    }
}