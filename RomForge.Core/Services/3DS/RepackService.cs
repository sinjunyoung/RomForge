using _3DS.Core.Crypto;
using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using _3DS.Core.Services;
using Common;
using NSW.Utils;
using RomForge.Core.Models._3DS;
using System.IO;

namespace RomForge.Core.Services._3DS;

public class RepackService(Action<string, LogLevel> log, Func<string?> getPatchPath)
{
    private readonly RepackOutputBuilder _outputBuilder = new(log);

    public async Task UnpackAsync(string inputPath, string unpackedPath, KeyStore keyStore, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        log("언팩 시작...", LogLevel.Highlight);

        await using var source = await OpenSourceAsync(inputPath, keyStore, ct);

        long totalBytes = 0;

        foreach (var content in source.Contents)
        {
            var (ncchStream, _) = await source.OpenContentDecrypted(content.ContentIndex);

            await using (ncchStream)
            {
                byte[] hdrBuf = new byte[NcchHeader.Size];

                await ncchStream.ReadExactlyAsync(hdrBuf, ct);

                var ncchHeader = NcchHeader.Parse(hdrBuf);

                totalBytes += ((long)ncchHeader.ExefsSize * 0x200) + ((long)ncchHeader.RomfsSize * 0x200);
            }
        }

        long accumulatedBytes = 0;

        foreach (var content in source.Contents)
        {
            int idx = content.ContentIndex;
            var (ncchStream, _) = await source.OpenContentDecrypted(idx);

            await using (ncchStream)
            {
                byte[] hdrBuf = new byte[NcchHeader.Size];

                await ncchStream.ReadExactlyAsync(hdrBuf, ct);

                var ncchHeader = NcchHeader.Parse(hdrBuf);

                ncchStream.Position = 0;

                var unpack = await NcchUnpacker.UnpackAsync(ncchStream, ncchHeader, ct);
                string partDir = Path.Combine(unpackedPath, $"partition{idx}");
                long lastPartitionCurrent = 0;
                Action<long, long>? partitionReporter = null;

                if (reporter != null && totalBytes > 0)
                {
                    partitionReporter = (current, total) =>
                    {
                        long delta = current - lastPartitionCurrent;

                        if (delta > 0)
                        {
                            accumulatedBytes += delta;
                            lastPartitionCurrent = current;
                            reporter(accumulatedBytes, totalBytes);
                        }
                    };
                }

                await NcchUnpacker.SaveToDirectoryAsync(ncchStream, unpack, partDir, content, partitionReporter, ct);
                log($"파티션 {idx} 언팩 완료", LogLevel.Ok);
            }
        }

        if (reporter != null && totalBytes > 0)
            reporter(totalBytes, totalBytes);
    }

    public async Task<string> RepackAsync(string unpackedPath, string outputPath, string? displayName, string? gameName, string? publisher = null, KeyStore? keyStore = null, RepackOutputFormat format = RepackOutputFormat.Cci, Action<long, long>? reporter = null, Action<string>? onOutputPathKnown = null, CancellationToken ct = default)
    {
        log("리팩 시작...", LogLevel.Highlight);

        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();
        var contentsList = new List<Contents>();
        int exefsPatchedCount = 0;
        PatchFolderFileSource? romfsPatchSource = null;
        byte[]? exHeaderPart0 = null;
        byte[]? exefsBlockPart0 = null;
        string? titleId = null;
        using var patchCtx = PatchSourceContext.Open(getPatchPath(), log);
        bool patchDirSpecified = patchCtx.HasSource;

        var partitionIndices = Directory.GetDirectories(unpackedPath, "partition*")
            .Select(Path.GetFileName)
            .Select(name => int.TryParse(name!.AsSpan("partition".Length), out int i) ? i : (int?)null)
            .Where(i => i.HasValue)
            .Select(i => i!.Value)
            .OrderBy(i => i)
            .ToList();

        foreach (int idx in partitionIndices)
        {
            string partDir = Path.Combine(unpackedPath, $"partition{idx}");
            string headerPath = Path.Combine(partDir, "header.bin");

            if (!File.Exists(headerPath))
                throw new FileNotFoundException($"header.bin 없음: {partDir}");

            byte[] headerRaw = await File.ReadAllBytesAsync(headerPath, ct);
            var ncchHeader = NcchHeader.Parse(headerRaw);
            string contentPath = Path.Combine(partDir, "content.bin");

            if (!File.Exists(contentPath))
                throw new FileNotFoundException($"content.bin 없음: {partDir}");

            byte[] contentRaw = await File.ReadAllBytesAsync(contentPath, ct);
            using var cms = new MemoryStream(contentRaw);
            using var cbr = new BinaryReader(cms);
            var contents = new Contents
            {
                ContentId = cbr.ReadUInt32(),
                ContentIndex = cbr.ReadUInt16(),
                ContentType = cbr.ReadUInt16(),
            };

            contentsList.Add(contents);

            byte[]? exHeader = null;
            byte[]? logo = null;
            byte[]? plainRegion = null;
            string exHeaderPath = Path.Combine(partDir, "exheader.bin");
            string logoPath = Path.Combine(partDir, "logo.bin");
            string plainPath = Path.Combine(partDir, "plain.bin");

            if (File.Exists(exHeaderPath))
                exHeader = await File.ReadAllBytesAsync(exHeaderPath, ct);

            if (File.Exists(logoPath))
                logo = await File.ReadAllBytesAsync(logoPath, ct);

            if (File.Exists(plainPath))
                plainRegion = await File.ReadAllBytesAsync(plainPath, ct);

            string exefsDir = Path.Combine(partDir, "exefs");
            var exefsFiles = Directory.Exists(exefsDir) ? ExeFsUnpacker.LoadFromDirectory(exefsDir) : [];
            byte[] exefsBlock = [];

            if (exefsFiles.Count > 0)
            {
                var exefsIndex = idx == 0 ? patchCtx.FindSubIndex("exefs") : null;
                var rootIndex = idx == 0 ? patchCtx.RootIndex() : null;
                var (data, patchedCount) = await ExeFsPacker.PackWithPatchAsync(exefsFiles, exefsIndex, exHeader, rootIndex, log, ct);

                exefsBlock = data;
                exefsPatchedCount += patchedCount;
            }

            if (idx == 0 && exefsBlock.Length > 0 && (!string.IsNullOrEmpty(gameName) || !string.IsNullOrEmpty(publisher)))
                RepackOutputBuilder.ApplySmdhToMemory(exefsBlock, gameName, publisher, log);

            if (idx == 0)
            {
                exHeaderPart0 = exHeader;
                exefsBlockPart0 = exefsBlock;
                titleId = ncchHeader.ProgramId.ToString("x16");
            }

            string romfsDir = Path.Combine(partDir, "romfs");
            RomFsUnpackResult? romfsResult = null;
            IRomFsFileSource? romfsSource = null;

            if (Directory.Exists(romfsDir))
            {
                romfsResult = RomFsFolderScanner.ScanFolderAsUnpackResult(romfsDir);

                IRomFsFileSource? patchSource = null;

                if (idx == 0)
                {
                    romfsPatchSource = patchCtx.CreateRomfsSource("romfs");
                    patchSource = romfsPatchSource;
                }

                romfsSource = new FolderRomFsFileSource(romfsDir, patchSource);
            }

            var unpackResult = new NcchUnpackResult
            {
                Header = ncchHeader,
                ExHeader = exHeader,
                Logo = logo,
                PlainRegion = plainRegion,
                ExeFs = null,
                RomFs = romfsResult,
            };

            repackedNcchs[idx] = (unpackResult, exefsBlock, Stream.Null, romfsResult, romfsSource);
        }

        if (repackedNcchs.Count == 0)
            throw new InvalidOperationException("언팩된 파티션이 없습니다.");

        string safeFileName = NspNameBuilder.SafeFileName(displayName);
        string fileName = string.IsNullOrEmpty(safeFileName) ? "output" : safeFileName;
        string namePart = string.IsNullOrEmpty(titleId) ? fileName : $"{fileName} [{titleId.ToUpperInvariant()}]";
        string outputCci = Utils.GetUniqueFilePath(Path.Combine(outputPath, namePart + "_Repack.cci"));
        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, contentsList, log, ct);
        string outputFilePath = await _outputBuilder.BuildOutputAsync(repackedSource, outputCci, keyStore, format, exHeaderPart0, exefsBlockPart0, reporter, onOutputPathKnown, ct);

        if (patchDirSpecified && exefsPatchedCount == 0 && (romfsPatchSource == null || romfsPatchSource.AppliedCount == 0))
            log("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
        else
        {
            if (exefsPatchedCount > 0)
                log($"exefs 패치 적용 완료: {exefsPatchedCount}개 파일", LogLevel.Ok);

            if (romfsPatchSource is { AppliedCount: > 0 })
                log($"romfs 패치 적용 완료: {romfsPatchSource.AppliedCount}개 파일", LogLevel.Ok);
        }

        log($"완료: {outputFilePath}", LogLevel.Ok);

        return outputFilePath;
    }

    public async Task<string> RepackDirectAsync(string inputPath, string outputCci, KeyStore keyStore, string? gameName = null, string? publisher = null, RepackOutputFormat format = RepackOutputFormat.Cci, Action<long, long>? reporter = null, Action<string>? onOutputPathKnown = null, CancellationToken ct = default)
    {
        log("스트리밍 기반 리팩 시작...", LogLevel.Highlight);

        await using var source = await OpenSourceAsync(inputPath, keyStore, ct);
        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();
        int exefsPatchedCount = 0;
        PatchFolderFileSource? romfsPatchSource = null;
        byte[]? exHeaderPart0 = null;
        byte[]? exefsBlockPart0 = null;
        using var patchCtx = PatchSourceContext.Open(getPatchPath(), log);
        bool patchDirSpecified = patchCtx.HasSource;

        foreach (var content in source.Contents)
        {
            int idx = content.ContentIndex;
            var (ncchStream, _) = await source.OpenContentDecrypted(idx);
            byte[] hdrBuf = new byte[NcchHeader.Size];

            await ncchStream.ReadExactlyAsync(hdrBuf, ct);

            var ncchHeader = NcchHeader.Parse(hdrBuf);

            ncchStream.Position = 0;

            var unpack = await NcchUnpacker.UnpackAsync(ncchStream, ncchHeader, ct);

            byte[] exefsBlock = [];

            if (unpack.ExeFs != null)
            {
                IReadOnlyList<ExeFsFile> exefsSourceFiles = unpack.ExeFs.Files;

                var exefsIndex = idx == 0 ? patchCtx.FindSubIndex("exefs") : null;
                var rootIndex = idx == 0 ? patchCtx.RootIndex() : null;
                var (data, patchedCount) = await ExeFsPacker.PackWithPatchAsync(exefsSourceFiles, exefsIndex, unpack.ExHeader, rootIndex, log, ct);

                exefsBlock = data;
                exefsPatchedCount += patchedCount;
            }

            if (idx == 0 && exefsBlock.Length > 0 && (!string.IsNullOrEmpty(gameName) || !string.IsNullOrEmpty(publisher)))
                RepackOutputBuilder.ApplySmdhToMemory(exefsBlock, gameName, publisher, log);

            if (idx == 0)
            {
                exHeaderPart0 = unpack.ExHeader;
                exefsBlockPart0 = exefsBlock;
            }

            IRomFsFileSource? patchSource = null;

            if (idx == 0)
            {
                romfsPatchSource = patchCtx.CreateRomfsSource("romfs");
                patchSource = romfsPatchSource;
            }

            repackedNcchs[idx] = (unpack, exefsBlock, ncchStream, unpack.RomFs, patchSource);
        }

        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, source.Contents, log, ct);
        string outputFilePath = await _outputBuilder.BuildOutputAsync(repackedSource, outputCci, keyStore, format, exHeaderPart0, exefsBlockPart0, reporter, onOutputPathKnown, ct);

        if (patchDirSpecified && exefsPatchedCount == 0 && (romfsPatchSource == null || romfsPatchSource.AppliedCount == 0))
            log("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
        else
        {
            if (exefsPatchedCount > 0)
                log($"ExeFS 패치 적용 완료: {exefsPatchedCount}개 파일", LogLevel.Ok);

            if (romfsPatchSource is { AppliedCount: > 0 })
                log($"RomFS 패치 적용 완료: {romfsPatchSource.AppliedCount}개 파일", LogLevel.Ok);
        }

        log($"완료: {outputFilePath}", LogLevel.Ok);

        return outputFilePath;
    }

    private async Task<INcsdSource> OpenSourceAsync(string inputPath, KeyStore keyStore, CancellationToken ct)
    {
        string ext = Path.GetExtension(inputPath).ToLowerInvariant();

        return ext switch
        {
            ".cia" => await new CiaReader(keyStore).OpenAsync(inputPath, (msg, level) => log(msg, level), ct),
            ".cci" or ".zcci" or ".3ds" => await CciSource.OpenAsync(inputPath, keyStore, (msg, level) => log(msg, level), ct),
            _ => throw new NotSupportedException($"지원하지 않는 파일 형식: {ext}")
        };
    }
}