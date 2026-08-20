using Common;
using NSW.Utils;
using Patch.Core;
using Patch.Core.Services;
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
    private static readonly string[] PatchAnchors = ["content", "meta", "code"];

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
                foreach (var s in sources)
                    s.Dispose();
            }
        }, ct);
    }

    public static TitleInputEntry PeekFolder(string folderPath)
    {
        using ITitleSource source = WupTitleSource.LooksLikeWupFolder(folderPath) ? new WupTitleSource(folderPath) : new FolderTitleSource(folderPath);

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
            try
            {
                var row = PeekFolder(dir);
                string dirName = Path.GetFileName(dir);

                if (dirName.Contains("_update", StringComparison.OrdinalIgnoreCase))
                    row.Role = TitleRole.Update;
                else if (dirName.Contains("_dlc", StringComparison.OrdinalIgnoreCase))
                    row.Role = TitleRole.Dlc;

                rows.Add(row);
            }
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
            var meta = source is WupTitleSource ? WiiUMetadataExtractor.ExtractFromTitleSource(source) : WiiUMetadataExtractor.ExtractFromFolder(folderPath);

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

    public static ITitleSource ReopenSource(TitleInputEntry entry, string keysTxtPath)
    {
        if (entry.IsFolder)
            return WupTitleSource.LooksLikeWupFolder(entry.FilePath) ? new WupTitleSource(entry.FilePath) : new FolderTitleSource(entry.FilePath);

        var sources = UnpackService.OpenAll(entry.FilePath, keysTxtPath);

        for (int i = 0; i < sources.Count; i++)
            if (i != entry.SubTitleIndex)
                sources[i].Dispose();

        return sources[entry.SubTitleIndex];
    }

    public static (List<WupFileEntry> Files, IDisposable? PatchZipHandle) BuildFileEntries(ITitleSource source, string? patchPath, Action<string, LogLevel>? log = null)
    {
        var result = new List<WupFileEntry>();
        var overwriteFiles = new Dictionary<string, PatchFileRef>(StringComparer.Ordinal);
        var binaryPatches = new Dictionary<string, PatchFileRef>(StringComparer.Ordinal);

        IArchivePatchSource? archive = null;

        if (patchPath is not null)
        {
            PatchFileIndex index;

            if (ArchivePatchSourceFactory.IsArchivePath(patchPath))
            {
                archive = ArchivePatchSourceFactory.Open(patchPath);

                string prefix = ArchivePatchFolderResolver.FindPatchRoot(archive.EntryPaths, PatchAnchors) ?? "";

                index = PatchFileIndex.Build(archive, prefix);
            }
            else
            {
                string effectivePatchFolder = PatchFolderResolver.FindPatchRoot(patchPath, PatchAnchors) ?? patchPath;

                index = PatchFileIndex.Build(effectivePatchFolder);
            }

            foreach (var patchEntry in index.Entries)
            {
                string relDir = patchEntry.RelativeDir.Replace(Path.DirectorySeparatorChar, '/');
                string relTarget = relDir.Length == 0 ? patchEntry.BaseName : $"{relDir}/{patchEntry.BaseName}";

                if (patchEntry.Kind == PatchFileKind.BinaryPatch)
                    binaryPatches[relTarget] = patchEntry.File;
                else
                    overwriteFiles[relTarget] = patchEntry.File;
            }
        }

        var sourceFiles = new HashSet<string>(source.EnumerateFiles(), StringComparer.Ordinal);
        int overwriteMatched = overwriteFiles.Keys.Count(sourceFiles.Contains);
        int binaryMatched = binaryPatches.Keys.Count(sourceFiles.Contains);

        if (patchPath is not null && overwriteMatched == 0 && binaryMatched == 0)
            log?.Invoke("패치 대상 파일이 존재하지 않습니다.", LogLevel.Error);
        else if (patchPath is not null)
        {
            if (overwriteMatched > 0)
                log?.Invoke($"교체 완료: {overwriteMatched}개 파일", LogLevel.Ok);

            if (binaryMatched > 0)
                log?.Invoke($"패치 적용 완료: {binaryMatched}개 파일", LogLevel.Ok);
        }

        foreach (string relPath in sourceFiles)
        {
            string capturedRelPath = relPath;
            ITitleSource capturedSource = source;

            if (overwriteFiles.TryGetValue(relPath, out var overwriteRef))
            {
                log?.Invoke($"  교체: {overwriteRef.DisplayName} → {relPath}", LogLevel.Ok);

                result.Add(new WupFileEntry(relPath, overwriteRef.OpenRead, overwriteRef.Length));
                continue;
            }

            if (binaryPatches.TryGetValue(relPath, out var patchRef))
            {
                byte[] originalData;
                using (var srcStream = source.OpenRead(relPath))
                using (var ms = new MemoryStream())
                {
                    srcStream.CopyTo(ms);
                    originalData = ms.ToArray();
                }

                byte[] patchData = patchRef.ReadSmallFileBytes();
                byte[] patchedData = UniversalPatcher.ApplyPatchAsync(originalData, patchData, null).GetAwaiter().GetResult();

                log?.Invoke($"  패치 완료: {patchRef.DisplayName} → {relPath}", LogLevel.Info);

                result.Add(new WupFileEntry(relPath, () => new MemoryStream(patchedData), patchedData.Length));
                continue;
            }

            result.Add(new WupFileEntry(relPath, () => capturedSource.OpenRead(capturedRelPath), capturedSource.GetFileSize(capturedRelPath)));
        }

        return (result, archive);
    }

    public static async Task UnpackAsync(IReadOnlyList<TitleInputEntry> entries, string keysTxtPath, string outputPath, Action<ProgressInfo>? progress = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            string unpackedRoot = Path.Combine(outputPath, "unpacked");
            var sw = Stopwatch.StartNew();

            log?.Invoke("언팩 시작...", LogLevel.Highlight);

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                using var source = ReopenSource(entry, keysTxtPath);

                log?.Invoke($"[{entry.Kind}] {source.TitleIdHex}_v{source.TitleVersion} 언팩 중...", LogLevel.Info);

                string roleSuffix = entry.Role switch
                {
                    TitleRole.Update => "_update",
                    TitleRole.Dlc => "_dlc",
                    _ => "",
                };

                string destFolder = Utils.GetUniqueFilePath(Path.Combine(unpackedRoot, $"{source.TitleIdHex}_v{source.TitleVersion}{roleSuffix}"));

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

            log?.Invoke("리팩 시작...", LogLevel.Highlight);

            try
            {
                var repackEntries = new List<RepackEntry>();

                for (int i = 0; i < entries.Count; i++)
                    repackEntries.Add(new RepackEntry(sources[i], entries[i].PatchPath, TitleIdHexOverride: entries[i].RoleCorrectedTitleIdHex));

                if (format == RepackOutputFormat.Wup)
                {
                    RepackToWup(repackEntries, sources, entries, outputPath, progress, log, ct);
                    return;
                }

                string fileName = BuildWuaFileName(entries);
                string outputWuaPath = Utils.GetUniqueFilePath(Path.Combine(outputPath, $"{fileName}.wua"));
                var sw = Stopwatch.StartNew();

                try
                {
                    WiiURepackService.RepackMultiple(repackEntries, outputWuaPath,
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
                        log: log,
                        ct: ct);

                    log?.Invoke($"완료: {outputWuaPath}", LogLevel.Ok);
                }
                catch
                {
                    if (File.Exists(outputWuaPath))
                    {
                        try { File.Delete(outputWuaPath); } catch { }
                    }

                    throw;
                }
            }
            finally
            {
                foreach (var s in sources)
                    s.Dispose();
            }
        }, ct);
    }

    private static string BuildWuaFileName(IReadOnlyList<TitleInputEntry> entries)
    {
        var baseEntry = entries.FirstOrDefault(e => e.Role != TitleRole.Update && e.Role != TitleRole.Dlc) ?? entries[0];
        string titleName = baseEntry.TitleName ?? baseEntry.DisplayName;
        string titleIdHex = baseEntry.TitleIdHex;
        int baseCount = entries.Count(e => e.Role != TitleRole.Update && e.Role != TitleRole.Dlc);
        int updateCount = entries.Count(e => e.Role == TitleRole.Update);
        int dlcCount = entries.Count(e => e.Role == TitleRole.Dlc);
        var parts = new List<string>();

        if (baseCount > 0)
            parts.Add(baseCount > 1 ? $"{baseCount}B" : "B");

        if (updateCount > 0)
            parts.Add(updateCount > 1 ? $"{updateCount}U" : "U");

        if (dlcCount > 0)
            parts.Add(dlcCount > 1 ? $"{dlcCount}D" : "D");

        string comp = string.Join("+", parts);
        string safeName = NspNameBuilder.SafeFileName(titleName);

        return $"{safeName} [{titleIdHex}] ({comp})";
    }

    private static void RepackToWup(List<RepackEntry> repackEntries, List<ITitleSource> sources, IReadOnlyList<TitleInputEntry> entries, string outputPath, Action<ProgressInfo>? progress, Action<string, LogLevel>? log, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < sources.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var source = sources[i];
            string? patchPath = repackEntries[i].PatchFolder;
            var (files, patchZipHandle) = BuildFileEntries(source, patchPath, log);
            using var _ = patchZipHandle;
            ulong titleId = entries[i].GetRoleCorrectedTitleId();
            ushort titleVersion = (ushort)source.TitleVersion;
            var folderName = BuildWupFolderName(entries[i]);
            string wupFolder = Utils.GetUniqueFolderPath(Path.Combine(outputPath, $"{folderName}"));

            log?.Invoke($"WUP로 패키징 중 ({i + 1}/{sources.Count}): {wupFolder}", LogLevel.Info);

            try
            {
                WupPacker.Pack(wupFolder, titleId, titleVersion, files,
                    onProgress: (done, total, label) =>
                    {
                        progress?.Invoke(new ProgressInfo
                        {
                            Percent = total > 0 ? (int)(done * 100.0 / total) : 100,
                            Label = $"[{i + 1}/{sources.Count}] {label}",
                            TimeInfo = $"{sw.Elapsed:mm\\:ss} 경과",
                            Speed = string.Empty,
                        });
                    },
                    ct: ct);
            }
            catch
            {
                if (Directory.Exists(wupFolder))
                {
                    try { Directory.Delete(wupFolder, true); } catch { }
                }
                throw;
            }

            progress?.Invoke(new ProgressInfo
            {
                Percent = (int)((i + 1) * 100.0 / sources.Count),
                Label = $"WUP 생성 완료: {titleId:x16}_v{titleVersion}",
                TimeInfo = string.Empty,
                Speed = string.Empty,
            });

            log?.Invoke($"완료: {wupFolder}", LogLevel.Ok);
        }
    }

    private static string BuildWupFolderName(TitleInputEntry entry)
    {
        string safeName = NspNameBuilder.SafeFileName(entry.TitleName ?? entry.DisplayName);
        string titleIdHex = entry.TitleIdHex.ToUpper();

        string roleTag = entry.Role switch
        {
            TitleRole.Update => "Update",
            TitleRole.Dlc => "DLC",
            _ => "Game"
        };

        return $"{safeName} [{roleTag}] [{titleIdHex}]";
    }
}