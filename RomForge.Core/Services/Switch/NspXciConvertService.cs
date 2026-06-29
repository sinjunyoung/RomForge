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
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace RomZip.Core.Services;

public static class NspXciConvertService
{
    private const long XciHfs0HeaderSizePos = 0x138;
    private const long XciHfs0HeaderHashPos = 0x140;
    private const ulong MediaSize = 0x200;

    public static Task<string> NspToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunNspToXciAsync(inputPath, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunXciToNspAsync(inputPath, keySet.Clone(), progress, log, ct);
    }

    #region NSP → XCI

    private static async Task<string> RunNspToXciAsync(string inputPath, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        string? finalPath = null;
        bool isCompleted = false;

        log?.Invoke($"{Path.GetFileName(inputPath)} NSP → XCI 변환 시작", LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);
            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);
            disposables.Add(sourceStorage);

            IFileSystem sourceFs = sourceStorage.OpenFileSystem(keySet, inputPath);
            disposables.Add(sourceFs);
            keySet.RegisterTickets(sourceFs);

            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, ".xci"));

            string displayName = $"NSP → XCI {NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";

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
                var capStorage = ncaStorage;

                fileEntries.Add((ncaName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(capStorage.AsStream(), s, (int)nca.Header.KeyGeneration, keySet, onRead, ct);
                }, ncaSize, label));
            }

            // XCI 템플릿에서 prefix 읽기 (XCI 헤더가 없으므로 빈 512바이트 prefix 생성)
            // XCI prefix는 0x130 오프셋에 hfs0StartOffset이 있어야 함 — 실제 XCI 원본이 없으므로
            // secure 파티션만 감싸는 최소 구조로 작성
            byte[] xciPrefixBuffer = BuildMinimalXciPrefix();

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);
            disposables.Add(fout);

            await fout.WriteAsync(xciPrefixBuffer, ct);

            long rootHeaderPos = fout.Position;
            var rootBuilderTemp = new Hfs0Builder();
            rootBuilderTemp.AddFile("secure", 0, new byte[32], 0);
            int rootHeaderSize = (int)rootBuilderTemp.AlignedHeaderSize(MediaSize);
            await fout.WriteAsync(new byte[rootHeaderSize], ct);

            long rootDataStart = rootHeaderPos + rootHeaderSize;
            long secureAbsStart = fout.Position;

            var secureBuilderTemp = new Hfs0Builder();
            foreach (var (name, _, estimatedSize, _) in fileEntries)
                secureBuilderTemp.AddFile(name, (ulong)estimatedSize, new byte[32], 0x200);

            int secureHeaderSize = (int)secureBuilderTemp.AlignedHeaderSize(MediaSize);
            await fout.WriteAsync(new byte[secureHeaderSize], ct);

            long secureDataStart = fout.Position;
            long totalEstimated = fileEntries.Sum(f => f.EstimatedSize);
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
                    var (_, writer, _, _) = fileEntries[i];
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

            progress?.Report(new ProgressInfo(100, displayName, meta.TitleId, string.Empty, string.Empty));

            long finalEndPos = fout.Position;

            // secure HFS0 헤더 역기입
            var secureBuilder = new Hfs0Builder();
            for (int i = 0; i < fileEntries.Count; i++)
                secureBuilder.AddFile(fileEntries[i].Name, actualSizes[i], actualHashes[i], 0x200);

            byte[] secureHeader = secureBuilder.BuildHeader(MediaSize);
            byte[] secureHash = SHA256.HashData(secureHeader);
            ulong secureTotal = (ulong)(finalEndPos - secureAbsStart);

            fout.Position = secureAbsStart;
            await fout.WriteAsync(secureHeader, ct);

            // root HFS0 헤더 역기입
            var rootBuilder = new Hfs0Builder();
            rootBuilder.AddFileWithOffset("secure", 0, secureTotal, secureHash, (uint)secureHeader.Length);
            byte[] rootHeader = rootBuilder.BuildHeader(MediaSize);
            byte[] rootHash = SHA256.HashData(rootHeader);

            fout.Position = rootHeaderPos;
            await fout.WriteAsync(rootHeader, ct);

            // XCI prefix의 hfs0 크기/해시 패치
            BinaryPrimitives.WriteInt64LittleEndian(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderSizePos), rootHeader.Length);
            rootHash.CopyTo(xciPrefixBuffer.AsSpan((int)XciHfs0HeaderHashPos));
            fout.Position = 0;
            await fout.WriteAsync(xciPrefixBuffer, ct);
            fout.Position = finalEndPos;
            await fout.FlushAsync(ct);

            isCompleted = true;
            log?.Invoke($"NSP → XCI 변환 완료: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();
            if (!isCompleted && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                try { File.Delete(finalPath); } catch { }
        }
    }

    #endregion

    #region XCI → NSP

    private static async Task<string> RunXciToNspAsync(string inputPath, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, CancellationToken ct)
    {
        var disposables = new List<IDisposable>();
        string? finalPath = null;
        bool isCompleted = false;

        log?.Invoke($"{Path.GetFileName(inputPath)} XCI → NSP 변환 시작", LogLevel.Info, inputPath);

        try
        {
            var metas = MetadataReader.GetMetadataFromContainer(keySet, inputPath);
            if (metas.Count == 0)
                throw new InvalidOperationException(Res.Error_NoMetadata);

            var meta = metas.First();
            var sourceStorage = new LocalStorage(inputPath, FileAccess.Read);
            disposables.Add(sourceStorage);

            var xci = new Xci(keySet, sourceStorage);
            var securePartition = xci.OpenPartition(XciPartitionType.Secure);
            disposables.Add(securePartition);
            keySet.RegisterTickets(securePartition);

            finalPath = Utils.GetUniqueFilePath(Path.ChangeExtension(inputPath, ".nsp"));

            string displayName = $"XCI → NSP {NspNameBuilder.CompressDisplayNameBuild(meta.KrTitle, meta.TitleId, meta.DisplayVersion)}";

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
                var capStorage = ncaStorage;

                fileEntries.Add((ncaName, async (s, onRead) =>
                {
                    await NcaRecryptService.RecryptAsync(capStorage.AsStream(), s, (int)nca.Header.KeyGeneration, keySet, onRead, ct);
                }, ncaSize, label));
            }

            var fout = File.Open(finalPath, FileMode.Create, FileAccess.ReadWrite);
            disposables.Add(fout);

            long totalEstimated = fileEntries.Sum(f => f.EstimatedSize);
            var reporter = new ProgressReporter(displayName, meta.TitleId, totalEstimated, progress);
            void onRead(long bytesRead) => reporter.AddProgress(bytesRead);

            using var timer = new System.Timers.Timer(200);
            timer.Elapsed += (_, _) => reporter.ForceReport();
            timer.AutoReset = true;
            timer.Start();

            try
            {
                await Pfs0Builder.WriteAsync(displayName, Path.GetFileNameWithoutExtension(finalPath), fileEntries, fout, Pfs0Builder.GetAlignmentPadding(inputPath), progress, ct);
            }
            finally
            {
                timer.Stop();
            }

            await fout.FlushAsync(ct);

            isCompleted = true;
            log?.Invoke($"XCI → NSP 변환 완료: {Path.GetFileName(finalPath)}", LogLevel.Ok, meta.TitleId);

            return finalPath;
        }
        finally
        {
            for (int i = disposables.Count - 1; i >= 0; i--) disposables[i]?.Dispose();
            if (!isCompleted && !string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
                try { File.Delete(finalPath); } catch { }
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// NSP 원본에서 XCI로 변환 시 XCI 카트리지 헤더가 없으므로
    /// 최소한의 유효한 XCI prefix 버퍼(hfs0StartOffset 위치까지)를 생성합니다.
    /// Magic: HEAD(0x48454144), hfs0StartOffset: 0x148(prefix 크기)
    /// </summary>
    private static byte[] BuildMinimalXciPrefix()
    {
        // XCI prefix 크기: 0x148 (hfs0StartOffset 값과 동일)
        const int prefixSize = 0x148;
        byte[] buf = new byte[prefixSize];

        // Magic "HEAD"
        buf[0x100] = 0x48; // H
        buf[0x101] = 0x45; // E
        buf[0x102] = 0x41; // A
        buf[0x103] = 0x44; // D

        // hfs0StartOffset = prefixSize
        BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(0x130), prefixSize);

        return buf;
    }

    #endregion
}