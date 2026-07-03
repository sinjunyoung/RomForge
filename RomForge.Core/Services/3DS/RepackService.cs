using _3DS.Core.Crypto;
using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using _3DS.Core.Services;
using Common;
using NSW.Utils;
using System.IO;

namespace RomForge.Core.Services._3DS;

public class RepackService(Action<string, LogLevel> log, Func<string?> getPatchPath)
{
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

                log($"파티션 {idx} 언팩 완료", LogLevel.Info);
            }
        }

        if (reporter != null && totalBytes > 0)
            reporter(totalBytes, totalBytes);
    }

    public async Task RepackAsync(string unpackedPath, string outputPath, string? gameName, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        log("리팩 시작...", LogLevel.Highlight);

        string safeFileName = NspNameBuilder.SafeFileName(gameName);
        string fileName = string.IsNullOrEmpty(safeFileName) ? "output" : safeFileName;        
        string outputCci = Utils.GetUniqueFilePath(Path.Combine(outputPath, fileName + "_Repack.cci"));
        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();
        var contentsList = new List<Contents>();
        int idx = 0;

        while (true)
        {
            string partDir = Path.Combine(unpackedPath, $"partition{idx}");

            if (!Directory.Exists(partDir))
                break;

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

            string? exefsPatchDir = idx == 0 ? GetPatchDir("exefs") : null;
            string exefsDir = Path.Combine(partDir, "exefs");
            var exefsFiles = Directory.Exists(exefsDir) ? ExeFsUnpacker.LoadFromDirectory(exefsDir) : [];
            byte[] exefsBlock = exefsFiles.Count > 0 ? await ExeFsPacker.PackWithPatchAsync(exefsFiles, exefsPatchDir, exHeader, getPatchPath(), ct) : [];
            string? romfsPatchDir = idx == 0 ? GetPatchDir("romfs") : null;
            string romfsDir = Path.Combine(partDir, "romfs");
            RomFsUnpackResult? romfsResult = null;
            IRomFsFileSource? romfsSource = null;

            if (Directory.Exists(romfsDir))
            {
                romfsResult = RomFsPacker.ScanFolderAsUnpackResult(romfsDir);
                IRomFsFileSource? patchSource = romfsPatchDir != null ? new PatchFolderFileSource(romfsPatchDir) : null;
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
            idx++;
        }

        if (repackedNcchs.Count == 0)
            throw new InvalidOperationException("언팩된 파티션이 없습니다.");

        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, contentsList, ct);

        await using var outputStream = File.Open(outputCci, FileMode.Create, FileAccess.ReadWrite);

        await NcsdBuilder.BuildAsync(repackedSource, outputStream, reporter, ct);

        log($"출력: {outputCci}", LogLevel.Ok);
    }

    public async Task RepackDirectAsync(string inputPath, string outputCci, KeyStore keyStore, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        log("메모리 기반 리팩 시작...", LogLevel.Highlight);

        await using var source = await OpenSourceAsync(inputPath, keyStore, ct);

        var repackedNcchs = new Dictionary<int, (NcchUnpackResult, byte[], Stream, RomFsUnpackResult?, IRomFsFileSource?)>();

        foreach (var content in source.Contents)
        {
            int idx = content.ContentIndex;
            var (ncchStream, _) = await source.OpenContentDecrypted(idx);
            byte[] hdrBuf = new byte[NcchHeader.Size];

            await ncchStream.ReadExactlyAsync(hdrBuf, ct);

            var ncchHeader = NcchHeader.Parse(hdrBuf);

            ncchStream.Position = 0;

            var unpack = await NcchUnpacker.UnpackAsync(ncchStream, ncchHeader, ct);

            string? exefsPatchDir = idx == 0 ? GetPatchDir("exefs") : null;
            string? romfsPatchDir = idx == 0 ? GetPatchDir("romfs") : null;
            byte[] exefsBlock = unpack.ExeFs != null ? await ExeFsPacker.PackWithPatchAsync(unpack.ExeFs.Files, exefsPatchDir, unpack.ExHeader, getPatchPath(), ct) : [];
            IRomFsFileSource? patchSource = romfsPatchDir != null ? new PatchFolderFileSource(romfsPatchDir) : null;

            repackedNcchs[idx] = (unpack, exefsBlock, ncchStream, unpack.RomFs, patchSource);
        }

        var repackedSource = await RepackedNcsdSource.CreateAsync(repackedNcchs, source.Contents, ct);

        await using var outputStream = File.Open(outputCci, FileMode.Create, FileAccess.ReadWrite);

        await NcsdBuilder.BuildAsync(repackedSource, outputStream, reporter, ct);

        log($"출력: {outputCci}", LogLevel.Ok);
    }

    private string? GetPatchDir(string subFolder)
    {
        string? patchPath = getPatchPath();

        if (string.IsNullOrEmpty(patchPath))
            return null;

        string path = Path.Combine(patchPath, subFolder);

        return Directory.Exists(path) ? path : null;
    }

    private async Task<INcsdSource> OpenSourceAsync(string inputPath, KeyStore keyStore, CancellationToken ct)
    {
        string ext = Path.GetExtension(inputPath).ToLowerInvariant();

        return ext switch
        {
            ".cia" => await new CiaReader(keyStore).OpenAsync(inputPath, (msg, level, _) => log(msg, level), ct),
            ".cci" or ".3ds" => await CciSource.OpenAsync(inputPath, keyStore, (msg, level, _) => log(msg, level), ct),
            _ => throw new NotSupportedException($"지원하지 않는 파일 형식: {ext}")
        };
    }
}