using Common;
using NSW.Utils;
using RomForge.Core.Models.WiiU;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WiiU.Core.Models;
using WiiU.Core.Services;

namespace RomForge.Core.Services.WiiU;

public sealed class RepackService()
{
    public static async Task<IReadOnlyList<TitleInputEntry>> PeekFileAsync(string path, string keysTxtPath, CancellationToken ct)
    {
        return await Task.Run(async () =>
        {
            var sources = UnpackService.OpenAll(path, keysTxtPath);

            try
            {
                var rows = new List<TitleInputEntry>();

                for (int i = 0; i < sources.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    rows.Add(await BuildRowAsync(path, keysTxtPath, isFolder: false, subTitleIndex: i, sources[i]));
                }

                return (IReadOnlyList<TitleInputEntry>)rows;
            }
            finally
            {
                foreach (var s in sources) s.Dispose();
            }
        }, ct);
    }

    public static TitleInputEntry PeekFolder(string folderPath)
    {
        using ITitleSource source = WupTitleSource.LooksLikeWupFolder(folderPath)
            ? new WupTitleSource(folderPath)
            : new FolderTitleSource(folderPath);

        return BuildRowFromFolder(folderPath, source);
    }

    public static IReadOnlyList<TitleInputEntry> ScanUnpacked(string outputPath, Action<string, LogLevel>? log = null)
    {
        string unpackedRoot = Path.Combine(outputPath, "unpacked");

        if (!Directory.Exists(unpackedRoot))
            return [];

        var rows = new List<TitleInputEntry>();

        foreach (string dir in Directory.EnumerateDirectories(unpackedRoot))
        {
            try { rows.Add(PeekFolder(dir)); }
            catch (Exception ex) { log($"'{dir}' 폴더를 읽지 못했습니다: {ex.Message}", LogLevel.Error); }
        }

        return rows;
    }

    private static async Task<TitleInputEntry> BuildRowAsync(string path, string keysTxtPath, bool isFolder, int subTitleIndex, ITitleSource source)
    {
        int fileCount = source.EnumerateFiles().Count();

        string? titleName = null;
        ImageSource? icon = null;

        try
        {
            var meta = await WiiUMetadataExtractor.Extract(path, keysTxtPath);

            if (meta is not null)
            {
                titleName = meta.Title;

                if (meta.Image is { Length: > 0 } pngBytes)
                    icon = TryLoadIcon(pngBytes);
            }
        }
        catch
        {
        }

        return new TitleInputEntry(path, source.TitleIdHex)
        {
            IsFolder = isFolder,
            SubTitleIndex = subTitleIndex,
            TitleVersion = source.TitleVersion,
            FileCount = fileCount,
            TitleName = titleName,
            Icon = icon,
        };
    }

    private static TitleInputEntry BuildRowFromFolder(string folderPath, ITitleSource source)
    {
        int fileCount = source.EnumerateFiles().Count();

        string? titleName = null;
        ImageSource? icon = null;

        try
        {
            var meta = source is WupTitleSource
                ? WiiUMetadataExtractor.ExtractFromTitleSource(source)
                : WiiUMetadataExtractor.ExtractFromFolder(folderPath);

            if (meta is not null)
            {
                titleName = meta.Title;

                if (meta.Image is { Length: > 0 } pngBytes)
                    icon = TryLoadIcon(pngBytes);
            }
        }
        catch
        {
        }

        return new TitleInputEntry(folderPath, source.TitleIdHex)
        {
            IsFolder = true,
            SubTitleIndex = 0,
            TitleVersion = source.TitleVersion,
            FileCount = fileCount,
            TitleName = titleName,
            Icon = icon,
        };
    }

    private static BitmapImage? TryLoadIcon(byte[] pngBytes)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static ITitleSource ReopenSource(TitleInputEntry entry, string keysTxtPath)
    {
        if (entry.IsFolder)
        {
            return WupTitleSource.LooksLikeWupFolder(entry.FilePath)
                ? new WupTitleSource(entry.FilePath)
                : new FolderTitleSource(entry.FilePath);
        }

        var sources = UnpackService.OpenAll(entry.FilePath, keysTxtPath);

        for (int i = 0; i < sources.Count; i++)
            if (i != entry.SubTitleIndex) sources[i].Dispose();

        return sources[entry.SubTitleIndex];
    }

    public static async Task UnpackAsync(IReadOnlyList<TitleInputEntry> entries, string keysTxtPath, string outputPath, Action<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            string unpackedRoot = Path.Combine(outputPath, "unpacked");
            var sw = Stopwatch.StartNew();

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                using var source = ReopenSource(entry, keysTxtPath);

                log($"[{entry.Kind}] {source.TitleIdHex}_v{source.TitleVersion} 언팩 중...", LogLevel.Info);

                string destFolder = Path.Combine(unpackedRoot, $"{source.TitleIdHex}_v{source.TitleVersion}");

                Directory.CreateDirectory(destFolder);

                source.ExtractTo(destFolder,
                    onFileProgress: (done, total, filePath) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Invoke(new ProgressInfo
                        {
                            Percent = total > 0 ? (int)(done * 100.0 / total) : 100,
                            Label = $"[{entry.Kind}] {filePath}",
                            TimeInfo = $"{sw.Elapsed:mm\\:ss} 경과",
                            Speed = string.Empty,
                        });
                    },
                    cancellationToken: ct);
            }
        }, ct);
    }

    public static async Task RepackAsync(IReadOnlyList<TitleInputEntry> entries, string keysTxtPath, string outputPath, RepackOutputFormat format, Action<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        if (entries.Count == 0)
            throw new InvalidOperationException("패킹할 타이틀이 없습니다.");

        await Task.Run(() =>
        {
            Directory.CreateDirectory(outputPath);
            var sources = entries.Select(e => ReopenSource(e, keysTxtPath)).ToList();
            try
            {
                var repackEntries = new List<RepackEntry>();
                for (int i = 0; i < entries.Count; i++)
                    repackEntries.Add(new RepackEntry(sources[i], entries[i].PatchPath));

                string fileName = entries[0].DisplayName;
                fileName = NspNameBuilder.SafeFileName(fileName);

                if (format == RepackOutputFormat.Wup)
                {
                    RepackToWup(repackEntries, sources, fileName, outputPath, progress, log, ct);
                    return;
                }

                string outputWuaPath = Utils.GetUniqueFilePath(Path.Combine(outputPath, $"{fileName}_Repack.wua"));

                var sw = Stopwatch.StartNew();
                WiiURepackService.RepackMultiple(
                    repackEntries,
                    outputWuaPath,
                    onFileProgress: (done, total, path) =>
                    {
                        progress?.Invoke(new ProgressInfo
                        {
                            Percent = total > 0 ? (int)(done * 100.0 / total) : 100,
                            Label = path,
                            TimeInfo = $"{sw.Elapsed:mm\\:ss} 경과",
                            Speed = string.Empty,
                        });
                    },
                    ct: ct);

                log?.Invoke($"완료: {outputWuaPath}", LogLevel.Ok);
            }
            finally
            {
                foreach (var s in sources)
                    s.Dispose();
            }
        }, ct);
    }

    /// <summary>
    /// WUA 리팩과 달리, WUP는 소스(베이스/업데이트/DLC 등) 개수만큼 별도의 WUP 폴더를 각각 만든다
    /// (실제 NUS 배포 방식대로 타이틀당 tmd/tik 세트 하나). 소스 하나마다:
    /// code/는 파일별로 개별 raw 콘텐츠, meta/는 파일별로 개별 hashed 콘텐츠, 그 외(content/ 등)는
    /// 하나의 hashed 콘텐츠로 묶는다 — NUSPacker의 기본 규칙과 동일한 방식.
    /// </summary>
    private static void RepackToWup(List<RepackEntry> repackEntries, List<ITitleSource> sources, string fileName, string outputPath, Action<ProgressInfo>? progress, Action<string, LogLevel>? log, CancellationToken ct)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var source = sources[i];
            string? patchPath = repackEntries[i].PatchFolder;

            var codeFiles = new List<WupFileEntry>();
            var metaFiles = new List<WupFileEntry>();
            var contentFiles = new List<WupFileEntry>();

            foreach (string relPath in source.EnumerateFiles())
            {
                ct.ThrowIfCancellationRequested();

                byte[] data;

                string? patchFilePath = patchPath is null ? null : Path.Combine(patchPath, relPath.Replace('/', Path.DirectorySeparatorChar));

                if (patchFilePath is not null && File.Exists(patchFilePath))
                {
                    data = File.ReadAllBytes(patchFilePath);
                }
                else
                {
                    using var stream = source.OpenRead(relPath);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    data = ms.ToArray();
                }

                var entry = new WupFileEntry(relPath, data);

                if (relPath.StartsWith("code/", StringComparison.OrdinalIgnoreCase))
                    codeFiles.Add(entry);
                else if (relPath.StartsWith("meta/", StringComparison.OrdinalIgnoreCase))
                    metaFiles.Add(entry);
                else
                    contentFiles.Add(entry);
            }

            var groups = new List<WupContentGroup>();

            foreach (var f in codeFiles)
                groups.Add(new WupContentGroup { Hashed = false, Files = [f] });

            foreach (var f in metaFiles)
                groups.Add(new WupContentGroup { Hashed = true, Files = [f] });

            if (contentFiles.Count > 0)
                groups.Add(new WupContentGroup { Hashed = true, Files = contentFiles });

            ulong titleId = Convert.ToUInt64(source.TitleIdHex, 16);
            ushort titleVersion = (ushort)source.TitleVersion;

            string suffix = sources.Count > 1 ? $"_{i}_{source.TitleIdHex}_v{titleVersion}" : "";
            string wupFolder = Utils.GetUniqueFilePath(Path.Combine(outputPath, $"{fileName}{suffix}_WUP"));

            log?.Invoke($"WUP로 패키징 중 ({i + 1}/{sources.Count}): {wupFolder}", LogLevel.Info);

            WupPacker.Pack(wupFolder, titleId, titleVersion, groups);

            progress?.Invoke(new ProgressInfo
            {
                Percent = (int)((i + 1) * 100.0 / sources.Count),
                Label = $"WUP 생성 완료: {source.TitleIdHex}_v{titleVersion}",
                TimeInfo = string.Empty,
                Speed = string.Empty,
            });

            log?.Invoke($"완료: {wupFolder}", LogLevel.Ok);
        }
    }
}