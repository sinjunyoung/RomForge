using CHD.Core.Services;
using Common;
using Patch.Core.Formats.DCP.Services;
using Patch.Core.Services;
using RomForge.Core.Models.Patch;
using SevenZip;
using SharpCompress.Archives;
using System.IO;

namespace RomForge.Core.Services.Patch;

public static class SourceArchiveExtractor
{
    private static readonly string[] SupportedExtensions = [".zip", ".7z", ".rar"];

    private const long TinySizeThresholdBytes = 64 * 1024;

    private static readonly HashSet<string> RomLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nsp", ".nsz", ".xci", ".xcz",
        ".3ds", ".cci", ".cxi", ".cia", ".app", ".srl",
        ".nds", ".dsi", ".ids",
        ".wud", ".wux", ".wua", ".wup", ".rpx",
        ".gcm", ".gcz", ".wbfs", ".wia", ".rvz",
        ".pbp", ".cso",
        ".iso", ".bin", ".cue", ".gdi", ".cdi", ".chd", ".nrg", ".mdf", ".mds", ".img", ".ccd", ".sub", ".cdr", ".toc",
        ".nes", ".fds", ".unf", ".unif",
        ".sfc", ".smc", ".fig", ".swc",
        ".n64", ".z64", ".v64", ".ndd",
        ".gb", ".gbc", ".gba",
        ".sms", ".gg", ".md", ".gen", ".smd", ".32x", ".sg",
        ".neo", ".ngp", ".ngc",
        ".a26", ".a52", ".a78", ".lnx", ".j64",
        ".col",
        ".pce", ".sgx",
        ".ws", ".wsc",
        ".xbe", ".xex", ".god",
        ".vpk", ".pkg"
    };

    private static readonly string[] IgnoredExtensions = [".txt", ".nfo", ".diz", ".url", ".ini", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".md5", ".sfv", ".log", ".pdf", ".doc", ".docx"];

    private static readonly string[] HashExcludedExtensions = [".cue", ".gdi", ".ccd", ".sub", ".mds", ".toc"];

    public static bool IsArchivePath(string? path) => !string.IsNullOrEmpty(path) && SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static Task<ArchiveExtractResult> AnalyzeAndExtractAsync(string archivePath, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var session = OpenSession(archivePath);
            var entries = session.Entries.Where(e => !e.IsDirectory).ToList();

            if (entries.Count == 0)
                throw new InvalidOperationException("압축 파일에 항목이 없습니다.");

            var cueEntries = entries.Where(e => string.Equals(Path.GetExtension(e.Key), ".cue", StringComparison.OrdinalIgnoreCase)).ToList();

            if (cueEntries.Count == 1)
                return ResolveCue(session, cueEntries[0], entries, extractDir, progress, ct);

            if (cueEntries.Count > 1)
            {
                return new ArchiveExtractResult
                {
                    Candidates = [.. cueEntries.Select(e => new ArchiveCandidate(e.Key, e.Size))]
                };
            }

            var gdiEntries = entries.Where(e => string.Equals(Path.GetExtension(e.Key), ".gdi", StringComparison.OrdinalIgnoreCase)).ToList();

            if (gdiEntries.Count == 1)
                return ResolveGdi(session, gdiEntries[0], entries, extractDir, progress, ct);

            if (gdiEntries.Count > 1)
            {
                return new ArchiveExtractResult
                {
                    Candidates = [.. gdiEntries.Select(e => new ArchiveCandidate(e.Key, e.Size))]
                };
            }

            var ccdEntries = entries.Where(e => string.Equals(Path.GetExtension(e.Key), ".ccd", StringComparison.OrdinalIgnoreCase)).ToList();

            if (ccdEntries.Count == 1)
                return ResolveCcd(session, ccdEntries[0], entries, extractDir, progress, ct);

            if (ccdEntries.Count > 1)
            {
                return new ArchiveExtractResult
                {
                    Candidates = [.. ccdEntries.Select(e => new ArchiveCandidate(e.Key, e.Size))]
                };
            }

            var candidates = entries.Where(e => RomLikeExtensions.Contains(Path.GetExtension(e.Key))).ToList();

            if (candidates.Count == 0)
            {
                candidates = [.. entries
                    .Where(e => !IgnoredExtensions.Contains(Path.GetExtension(e.Key), StringComparer.OrdinalIgnoreCase))
                    .Where(e => e.Size >= TinySizeThresholdBytes)];
            }

            if (candidates.Count == 0)
                candidates = [.. entries.Where(e => !IgnoredExtensions.Contains(Path.GetExtension(e.Key), StringComparer.OrdinalIgnoreCase))];

            if (candidates.Count == 0)
                throw new InvalidOperationException("압축 안에서 패치 대상으로 볼 만한 파일을 찾을 수 없습니다.");

            if (candidates.Count > 1)
            {
                return new ArchiveExtractResult
                {
                    Candidates = [.. candidates.Select(e => new ArchiveCandidate(e.Key, e.Size))]
                };
            }

            var extracted = ExtractEntries(session, candidates, extractDir, progress, ct);

            return new ArchiveExtractResult { ResolvedPath = extracted[candidates[0].Key] };
        }, ct);

    public static Task<string> ExtractCandidateAsync(string archivePath, string extractDir, string entryKey, IProgress<ProgressInfo> progress, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var session = OpenSession(archivePath);
            var entries = session.Entries.Where(e => !e.IsDirectory).ToList();
            var entry = entries.FirstOrDefault(e => string.Equals(e.Key, entryKey, StringComparison.Ordinal));

            if (entry.Key is null)
                throw new InvalidOperationException("선택한 파일을 압축 안에서 찾을 수 없습니다.");

            if (string.Equals(Path.GetExtension(entry.Key), ".cue", StringComparison.OrdinalIgnoreCase))
            {
                var cueResult = ResolveCue(session, entry, entries, extractDir, progress, ct);

                return cueResult.ResolvedPath!;
            }

            if (string.Equals(Path.GetExtension(entry.Key), ".gdi", StringComparison.OrdinalIgnoreCase))
            {
                var gdiResult = ResolveGdi(session, entry, entries, extractDir, progress, ct);

                return gdiResult.ResolvedPath!;
            }

            if (string.Equals(Path.GetExtension(entry.Key), ".ccd", StringComparison.OrdinalIgnoreCase))
            {
                var ccdResult = ResolveCcd(session, entry, entries, extractDir, progress, ct);

                return ccdResult.ResolvedPath!;
            }

            var extracted = ExtractEntries(session, [entry], extractDir, progress, ct);

            return extracted[entry.Key];
        }, ct);

    public const int MaxHashEntries = 5;

    public static Task<int> ComputeHashesAsync(string archivePath, Action<string, RomHashResult> onEntry, IProgress<double>? progress, CancellationToken ct) =>
        Task.Run(() =>
        {
            using var session = OpenSession(archivePath);

            var targets = PickHashTargets(session.Entries, out int skipped);

            long totalBytes = Math.Max(1, targets.Sum(e => e.Size));
            long doneBytes = 0;

            foreach (var target in targets)
            {
                ct.ThrowIfCancellationRequested();

                long baseBytes = doneBytes;
                long entrySize = target.Size;

                var entryProgress = progress is null
                    ? null
                    : new DirectProgress(value => progress.Report((baseBytes + value * entrySize) / totalBytes));

                var result = session.ComputeHash(target.Key, entryProgress, ct);

                doneBytes += entrySize;

                onEntry(target.Key, result);
            }

            return skipped;
        }, ct);

    private static List<ArchiveEntryInfo> PickHashTargets(IReadOnlyList<ArchiveEntryInfo> entries, out int skipped)
    {
        var dataFiles = entries
            .Where(e => !e.IsDirectory)
            .Where(e => !IgnoredExtensions.Contains(Path.GetExtension(e.Key), StringComparer.OrdinalIgnoreCase))
            .Where(e => !HashExcludedExtensions.Contains(Path.GetExtension(e.Key), StringComparer.OrdinalIgnoreCase))
            .ToList();

        var romLike = dataFiles.Where(e => RomLikeExtensions.Contains(Path.GetExtension(e.Key))).ToList();
        var pool = romLike.Count > 0 ? romLike : dataFiles;

        if (pool.Count == 0)
            throw new InvalidOperationException("압축 안에서 해시를 계산할 파일을 찾을 수 없습니다.");

        var ordered = pool.OrderByDescending(e => e.Size).ToList();

        skipped = Math.Max(0, ordered.Count - MaxHashEntries);

        return ordered.Take(MaxHashEntries).ToList();
    }

    private sealed class DirectProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }

    private static ArchiveExtractResult ResolveCue(IArchiveSession session, ArchiveEntryInfo cueEntry, List<ArchiveEntryInfo> entries, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        var cueExtracted = ExtractEntries(session, [cueEntry], extractDir, progress, ct);
        string cuePath = cueExtracted[cueEntry.Key];
        var referencedBins = ConversionSource.ParseBinsFromCue(cuePath);
        string cueDir = GetEntryDirectory(cueEntry.Key);
        var binEntries = new List<ArchiveEntryInfo>();

        foreach (var bin in referencedBins)
        {
            string binFileName = Path.GetFileName(bin);
            var match = entries.FirstOrDefault(e => GetEntryDirectory(e.Key) == cueDir && string.Equals(Path.GetFileName(e.Key), binFileName, StringComparison.OrdinalIgnoreCase));

            if (match.Key is not null)
                binEntries.Add(match);
        }

        if (binEntries.Count == 0)
            throw new InvalidOperationException("CUE 파일이 참조하는 BIN 파일을 압축 안에서 찾을 수 없습니다.");

        var binExtracted = ExtractEntries(session, binEntries, extractDir, progress, ct);
        int mainIndex = ConversionSource.ResolveMainDataTrackIndex(cuePath);
        string? mainBinFileName = mainIndex >= 0 && mainIndex < referencedBins.Count ? Path.GetFileName(referencedBins[mainIndex]) : null;
        var mainEntry = binEntries.FirstOrDefault(e => string.Equals(Path.GetFileName(e.Key), mainBinFileName, StringComparison.OrdinalIgnoreCase));
        string resolvedKey = mainEntry.Key ?? binEntries[0].Key;

        return new ArchiveExtractResult { ResolvedPath = binExtracted[resolvedKey] };
    }

    private static ArchiveExtractResult ResolveGdi(IArchiveSession session, ArchiveEntryInfo gdiEntry, List<ArchiveEntryInfo> entries, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        var gdiExtracted = ExtractEntries(session, [gdiEntry], extractDir, progress, ct);
        string gdiPath = gdiExtracted[gdiEntry.Key];
        var gdi = GdiFile.Parse(gdiPath);
        string gdiDir = GetEntryDirectory(gdiEntry.Key);
        var trackEntries = new List<ArchiveEntryInfo>();

        foreach (var track in gdi.Tracks)
        {
            var match = entries.FirstOrDefault(e => GetEntryDirectory(e.Key) == gdiDir && string.Equals(Path.GetFileName(e.Key), track.FileName, StringComparison.OrdinalIgnoreCase));

            if (match.Key is not null)
                trackEntries.Add(match);
        }

        if (trackEntries.Count == 0)
            throw new InvalidOperationException("GDI 파일이 참조하는 트랙 파일을 압축 안에서 찾을 수 없습니다.");

        var trackExtracted = ExtractEntries(session, trackEntries, extractDir, progress, ct);
        string mainTrackFileName = gdi.DataTrack.FileName;
        var mainEntry = trackEntries.FirstOrDefault(e => string.Equals(Path.GetFileName(e.Key), mainTrackFileName, StringComparison.OrdinalIgnoreCase));
        string resolvedKey = mainEntry.Key ?? trackEntries[0].Key;

        return new ArchiveExtractResult { ResolvedPath = trackExtracted[resolvedKey] };
    }

    private static ArchiveExtractResult ResolveCcd(IArchiveSession session, ArchiveEntryInfo ccdEntry, List<ArchiveEntryInfo> entries, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        string ccdDir = GetEntryDirectory(ccdEntry.Key);
        string ccdBaseName = Path.GetFileNameWithoutExtension(ccdEntry.Key);

        var companionEntries = entries
            .Where(e => GetEntryDirectory(e.Key) == ccdDir && string.Equals(Path.GetFileNameWithoutExtension(e.Key), ccdBaseName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var imgEntry = companionEntries.FirstOrDefault(e => string.Equals(Path.GetExtension(e.Key), ".img", StringComparison.OrdinalIgnoreCase));

        if (imgEntry.Key is null)
            throw new InvalidOperationException("CCD 파일과 짝을 이루는 IMG 파일을 압축 안에서 찾을 수 없습니다.");

        var extracted = ExtractEntries(session, companionEntries, extractDir, progress, ct);

        return new ArchiveExtractResult { ResolvedPath = extracted[imgEntry.Key] };
    }

    private static string GetEntryDirectory(string key)
    {
        string normalized = key.Replace('\\', '/');
        int lastSlash = normalized.LastIndexOf('/');

        return lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
    }

    private static Dictionary<string, string> ExtractEntries(IArchiveSession session, List<ArchiveEntryInfo> entries, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct) => session.Extract([.. entries.Select(e => e.Key)], extractDir, progress, ct);

    private static IArchiveSession OpenSession(string archivePath) => string.Equals(Path.GetExtension(archivePath), ".7z", StringComparison.OrdinalIgnoreCase) ? new NativeSevenZipSession(archivePath) : new SharpCompressSession(archivePath);

    private readonly record struct ArchiveEntryInfo(string Key, long Size, bool IsDirectory);

    private interface IArchiveSession : IDisposable
    {
        IReadOnlyList<ArchiveEntryInfo> Entries { get; }

        Dictionary<string, string> Extract(List<string> keys, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct);

        RomHashResult ComputeHash(string key, IProgress<double>? progress, CancellationToken ct);
    }

    private sealed class SharpCompressSession : IArchiveSession
    {
        private readonly IArchive _archive;
        private readonly Dictionary<string, IArchiveEntry> _byKey;

        public IReadOnlyList<ArchiveEntryInfo> Entries { get; }

        public SharpCompressSession(string archivePath)
        {
            _archive = ArchiveFactory.OpenArchive(archivePath);
            _byKey = new Dictionary<string, IArchiveEntry>(StringComparer.Ordinal);

            var entries = new List<ArchiveEntryInfo>();

            foreach (var entry in _archive.Entries)
            {
                if (entry.Key is null)
                    continue;

                _byKey[entry.Key] = entry;
                entries.Add(new ArchiveEntryInfo(entry.Key, entry.Size, entry.IsDirectory));
            }

            Entries = entries;
        }

        public Dictionary<string, string> Extract(List<string> keys, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct)
        {
            long totalBytes = keys.Sum(k => _byKey[k].Size);
            long writtenTotal = 0;
            var reporter = new ProgressReporter("원본 압축 해제 중...", string.Empty, totalBytes, progress);
            var report = reporter.CreateAction();
            var result = new Dictionary<string, string>();
            byte[] buffer = new byte[81920];

            foreach (var key in keys)
            {
                ct.ThrowIfCancellationRequested();

                var entry = _byKey[key];
                string relativePath = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                string destPath = Path.Combine(extractDir, relativePath);
                string? destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                using (var entryStream = entry.OpenEntryStream())
                using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;

                    while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();

                        destStream.Write(buffer, 0, bytesRead);
                        writtenTotal += bytesRead;

                        if (totalBytes > 0)
                            report(writtenTotal, totalBytes);
                    }
                }

                result[key] = destPath;
            }

            reporter.ForceReport();

            return result;
        }

        public RomHashResult ComputeHash(string key, IProgress<double>? progress, CancellationToken ct)
        {
            var entry = _byKey[key];

            using var entryStream = entry.OpenEntryStream();

            return RomHasher.HashStream(entryStream, entry.Size, progress, ct);
        }

        public void Dispose() => _archive.Dispose();
    }

    private sealed class NativeSevenZipSession : IArchiveSession
    {
        private readonly SevenZipExtractor _extractor;
        private readonly Dictionary<string, ArchiveFileInfo> _byKey;

        public IReadOnlyList<ArchiveEntryInfo> Entries { get; }

        public NativeSevenZipSession(string archivePath)
        {
            NativeSevenZip.EnsureInitialized();

            _extractor = new SevenZipExtractor(archivePath);
            _byKey = new Dictionary<string, ArchiveFileInfo>(StringComparer.Ordinal);

            var entries = new List<ArchiveEntryInfo>();

            foreach (var info in _extractor.ArchiveFileData)
            {
                string key = info.FileName.Replace('\\', '/');

                _byKey[key] = info;
                entries.Add(new ArchiveEntryInfo(key, (long)info.Size, info.IsDirectory));
            }

            Entries = entries;
        }

        public Dictionary<string, string> Extract(List<string> keys, string extractDir, IProgress<ProgressInfo> progress, CancellationToken ct)
        {
            long totalBytes = keys.Sum(k => (long)_byKey[k].Size);
            var reporter = new ProgressReporter("원본 압축 해제 중...", string.Empty, totalBytes, progress);

            void OnExtracting(object? sender, ProgressEventArgs e)
            {
                ct.ThrowIfCancellationRequested();
                reporter.ReportPercent(e.PercentDone / 100.0);
            }

            _extractor.Extracting += OnExtracting;

            try
            {
                int[] indexes = [.. keys.Select(k => (int)_byKey[k].Index)];

                _extractor.ExtractFiles(extractDir, indexes);
            }
            finally
            {
                _extractor.Extracting -= OnExtracting;
            }

            ct.ThrowIfCancellationRequested();
            reporter.ForceReport();

            var result = new Dictionary<string, string>();

            foreach (var key in keys)
            {
                string relativePath = key.Replace('/', Path.DirectorySeparatorChar);

                result[key] = Path.Combine(extractDir, relativePath);
            }

            return result;
        }

        public RomHashResult ComputeHash(string key, IProgress<double>? progress, CancellationToken ct)
        {
            var info = _byKey[key];

            using var sink = new RomHashSink((long)info.Size, progress, ct);

            _extractor.ExtractFile((int)info.Index, sink);

            return sink.Complete();
        }

        public void Dispose() => _extractor.Dispose();
    }
}