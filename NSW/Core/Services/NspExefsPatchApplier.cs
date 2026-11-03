using Common;
using NSW.HacPack;
using Patch.Core;
using Patch.Core.Interfaces;
using Patch.Core.Services;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class NspExefsPatchApplier
{
    private static (string BuildId, Func<byte[]> ReadIps) MakeArchiveIpsItem(IArchivePatchSource archive, string key)
    {
        string buildId = Path.GetFileNameWithoutExtension(key).TrimEnd('0');

        byte[] Read()
        {
            var entry = archive.FindEntry(key)!;
            using var src = entry.Open();
            using var ms = new MemoryStream();

            src.CopyTo(ms);

            return ms.ToArray();
        }

        return (buildId, Read);
    }

    public static int ApplyFolderExefsPatches(string patchPath, IFileStore? exefs, IProgress<ProgressInfo> progress, Action<string, LogLevel> log)
    {
        if (exefs == null || !Directory.Exists(patchPath))
            return 0;

        var openedArchives = new List<IArchivePatchSource>();
        var items = new List<(string BuildId, Func<byte[]> ReadIps)>();

        try
        {
            foreach (var f in Directory.EnumerateFiles(patchPath, "*.ips", SearchOption.AllDirectories))
            {
                string path = f;

                items.Add((Path.GetFileNameWithoutExtension(path).TrimEnd('0'), () => File.ReadAllBytes(path)));
            }

            var archivePaths = Directory.EnumerateFiles(patchPath, "*.zip", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(patchPath, "*.7z", SearchOption.AllDirectories));

            foreach (var archivePath in archivePaths)
            {
                IArchivePatchSource archive;

                try
                {
                    archive = ArchivePatchSourceFactory.Open(archivePath);
                }
                catch (Exception ex)
                {
                    log($"⚠️ 압축파일을 열 수 없음(암호 걸림/손상 등): {Path.GetFileName(archivePath)} — {ex.Message}", LogLevel.Info);
                    continue;
                }

                openedArchives.Add(archive);

                foreach (var key in archive.EntryPaths.Where(k => k.EndsWith(".ips", StringComparison.OrdinalIgnoreCase)))
                    items.Add(MakeArchiveIpsItem(archive, key));
            }

            if (items.Count == 0)
                return 0;

            log($"exefs_patches용 .ips 발견: {items.Count}개", LogLevel.Info);
            return ApplyExefsPatchesCore(items, exefs, progress, log);
        }
        finally
        {
            foreach (var a in openedArchives)
                a.Dispose();
        }
    }

    public static int ApplyArchiveExefsPatches(IArchivePatchSource archive, IFileStore? exefs, IProgress<ProgressInfo> progress, Action<string, LogLevel> log)
    {
        if (exefs == null)
            return 0;

        var ipsKeys = archive.EntryPaths.Where(k => k.EndsWith(".ips", StringComparison.OrdinalIgnoreCase)).ToList();

        if (ipsKeys.Count == 0)
            return 0;

        var items = ipsKeys.Select(k => MakeArchiveIpsItem(archive, k));

        return ApplyExefsPatchesCore(items, exefs, progress, log);
    }

    private static int ApplyExefsPatchesCore(IEnumerable<(string BuildId, Func<byte[]> ReadIps)> ipsItems, IFileStore exefs, IProgress<ProgressInfo> progress, Action<string, LogLevel> log)
    {
        var buildIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in exefs.EnumerateRelativeFiles().Where(f => !f.Contains('/')))
        {
            if (name.Equals("main.npdm", StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = exefs.OpenRead(name);

            if (!NsoTool.TryReadHeaderInfo(stream, out bool isNso, out string headerBuildId) || !isNso)
                continue;

            buildIdMap[headerBuildId] = name;
        }

        int count = 0;
        var processedBuildIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (buildId, readIps) in ipsItems)
        {
            if (!processedBuildIds.Add(buildId))
            {
                log($"⚠️ 중복 IPS 패치로 건너뜀: {buildId}.ips", LogLevel.Error);
                continue;
            }

            if (!buildIdMap.TryGetValue(buildId, out var targetNso))
            {
                log($"⚠️ exefs_patches 대상 NSO를 찾을 수 없음 (build id 불일치): {buildId}", LogLevel.Info);
                continue;
            }

            progress.ReportStatus($"exefs_patches 적용 중... ({targetNso})", -1);

            byte[] nso = exefs.ReadAllBytes(targetNso);
            uint originalFlags = BitConverter.ToUInt32(nso, 0x0C);
            bool wasCompressed = NsoTool.IsCompressed(nso);
            byte[] plain = wasCompressed ? NsoTool.DecompressToPlain(nso) : nso;
            byte[] patched = NsoMemoryImage.Apply(plain, image => UniversalPatcher.ApplyPatchAsync(image, readIps()).GetAwaiter().GetResult());
            byte[] final = NsoTool.RecompressFromPlain(patched, originalFlags);

            exefs.WriteAllBytes(targetNso, final);

            log($"exefs_patches 적용 완료: {targetNso} ⬅️ {buildId} (재압축: {wasCompressed})", LogLevel.Ok);

            count++;
        }

        return count;
    }
}