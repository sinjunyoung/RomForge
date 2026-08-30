using Common;
using System.IO.Compression;

namespace Patch.Core.Services.PC98;

public sealed record Pc98PatchResult(int AppliedCount, IReadOnlyList<string> MissingFiles, string MountPoint, string PatchSourceName);

public static class Pc98PatchService
{
    public static async Task<Pc98PatchResult> ApplyAsync(string hdiPath, string patchPath, string outputPath, Action<string, LogLevel>? log, IProgress<ProgressInfo>? progress, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            string? tempDir = null;

            try
            {
                string searchRoot = ResolveSearchRoot(patchPath, out tempDir);
                var image = new Fat16Image(hdiPath);
                var tree = image.Walk();

                log?.Invoke("HDI 볼륨 분석 완료", LogLevel.Info);

                var (patchDir, mountPoint, patchFiles, score) = ResolveBestSource(searchRoot, tree);

                if (score <= 0)
                    throw new InvalidOperationException("HDI 안에서 이 패치와 일치하는 파일을 하나도 찾지 못했습니다. 원본 HDI와 패치가 서로 다른 게임/버전일 수 있습니다.");

                log?.Invoke($"패치 소스 폴더 자동 감지: {Path.GetFileName(patchDir)} ({score}/{patchFiles.Count} 일치)", LogLevel.Info);
                log?.Invoke(string.IsNullOrEmpty(mountPoint) ? "마운트 경로: 루트" : $"마운트 경로 자동 감지: {mountPoint}", LogLevel.Info);

                string prefixKey = mountPoint.Length > 0 ? mountPoint + "/" : string.Empty;

                var lookup = new Dictionary<string, Fat16DirEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (var (path, entry) in tree)
                {
                    if (path.EndsWith('/'))
                        continue;

                    if (prefixKey.Length > 0 && !path.StartsWith(prefixKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    lookup[path[prefixKey.Length..]] = entry;
                }

                int applied = 0;
                var missing = new List<string>();
                int total = Math.Max(patchFiles.Count, 1);
                int done = 0;

                foreach (var rel in patchFiles)
                {
                    ct.ThrowIfCancellationRequested();

                    if (lookup.TryGetValue(rel, out var entry))
                    {
                        byte[] newData = File.ReadAllBytes(Path.Combine(patchDir, rel));

                        image.ReplaceFile(entry, newData);
                        applied++;
                        log?.Invoke($"패치 완료: {rel}", LogLevel.Info);
                    }
                    else
                    {
                        missing.Add(rel);
                        log?.Invoke($"HDI 안에서 찾을 수 없음: {rel}", LogLevel.Info);
                    }

                    done++;
                    progress?.Report(new ProgressInfo(done * 100 / total, rel, string.Empty, string.Empty, string.Empty));
                }

                image.Save(outputPath);

                return new Pc98PatchResult(applied, missing, mountPoint, Path.GetFileName(patchDir));
            }
            finally
            {
                if (tempDir is not null && Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }, ct);
    }

    private static string ResolveSearchRoot(string patchPath, out string? tempDir)
    {
        tempDir = null;

        if (Directory.Exists(patchPath))
            return patchPath;

        if (File.Exists(patchPath) && patchPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            tempDir = Path.Combine(Path.GetTempPath(), "pc98patch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(patchPath, tempDir);

            return tempDir;
        }

        throw new FileNotFoundException("패치 폴더 또는 ZIP을 찾을 수 없습니다.", patchPath);
    }

    private static (string PatchDir, string MountPoint, List<string> RelativeFiles, int Score) ResolveBestSource(string searchRoot, Dictionary<string, Fat16DirEntry> tree)
    {
        var candidateDirs = new List<string> { searchRoot };
        candidateDirs.AddRange(Directory.GetDirectories(searchRoot, "*", SearchOption.AllDirectories));

        string bestDir = searchRoot;
        string bestMount = string.Empty;
        List<string> bestFiles = [];
        int bestScore = -1;

        foreach (var dir in candidateDirs)
        {
            var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .ToList();

            if (files.Count == 0)
                continue;

            var (mount, score) = FindBestMountPoint(tree, files);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
                bestMount = mount;
                bestFiles = files;
            }
        }

        return (bestDir, bestMount, bestFiles, bestScore);
    }

    private static (string MountPoint, int Score) FindBestMountPoint(Dictionary<string, Fat16DirEntry> tree, List<string> patchFiles)
    {
        var dirPrefixes = new List<string> { string.Empty };

        foreach (var path in tree.Keys)
        {
            if (path.EndsWith('/'))
                dirPrefixes.Add(path[..^1]);
        }

        string bestPrefix = string.Empty;
        int bestScore = -1;

        foreach (var prefix in dirPrefixes)
        {
            string prefixKey = prefix.Length > 0 ? prefix + "/" : string.Empty;
            int score = 0;

            foreach (var rel in patchFiles)
            {
                if (tree.ContainsKey(prefixKey + rel))
                    score++;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestPrefix = prefix;
            }
        }

        return (bestPrefix, bestScore);
    }
}
