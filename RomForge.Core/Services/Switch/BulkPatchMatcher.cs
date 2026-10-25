using NSW.Core.ViewModels;
using Patch.Core.Interfaces;
using RomForge.Core.Models.Switch;
using System.IO;
using System.Text;

namespace RomForge.Core.Services.Switch;

public static class BulkPatchMatcher
{
    public static BulkPatchReport MatchFromFolder(IReadOnlyList<GameFile> targets, string rootFolder, Func<GameFile, IReadOnlyList<string>> expectedIds)
    {
        var source = PatchSource.FromFolder(rootFolder);
        return Match(targets, source, null, expectedIds, file => FindNamedArchive(rootFolder, file.TitleID));
    }

    public static BulkPatchReport MatchFromArchive(IReadOnlyList<GameFile> targets, IArchivePatchSource archive, string archivePath, string? password, Func<GameFile, IReadOnlyList<string>> expectedIds)
    {
        var source = PatchSource.FromArchive(archivePath, archive);
        return Match(targets, source, password, expectedIds, _ => null);
    }

    private static readonly string[] sourceArray = [".zip", ".7z"];

    private static string? FindNamedArchive(string rootFolder, string? titleId)
    {
        if (string.IsNullOrEmpty(titleId))
            return null;

        return sourceArray.Select(ext => Path.Combine(rootFolder, titleId + ext)).FirstOrDefault(File.Exists);
    }

    private static BulkPatchReport Match(IReadOnlyList<GameFile> targets, PatchSource source, string? password, Func<GameFile, IReadOnlyList<string>> expectedIds, Func<GameFile, string?> namedArchive)
    {
        var details = new StringBuilder();
        var allIds = new HashSet<string>(targets.SelectMany(expectedIds), StringComparer.OrdinalIgnoreCase);
        var baseRow = targets.FirstOrDefault(f => f.FileType.Contains('B'));
        int matched = 0;
        int dlcTotal = 0;
        int dlcMatched = 0;

        foreach (var file in targets)
        {
            var ids = expectedIds(file);
            var match = source.Match(ids);
            bool isDlc = file.FileType.Contains('D');
            bool isUpdate = !isDlc && file.FileType.Contains('U') && !file.FileType.Contains('B');
            string name = string.IsNullOrEmpty(file.TitleName) ? file.TitleID ?? string.Empty : file.TitleName;
            string? path = match.Path;
            string? pathPassword = password;
            string? note = null;

            if (isDlc)
                dlcTotal++;

            if (path == null && namedArchive(file) is { } named)
            {
                path = named;
                pathPassword = null;
                note = "타이틀 ID 이름의 압축파일로 지정";
            }

            if (path == null && !source.HasIdDirs && source.HasGeneric && ReferenceEquals(file, baseRow))
            {
                path = source.GenericPath;
                note = "타이틀 ID 폴더가 없어 일반 패치(exefs/romfs)로 베이스에 지정";
            }

            if (path == null)
            {
                if (!isDlc && !isUpdate)
                    details.AppendLine($"{name}: 일치하는 폴더 없음 (필요: {string.Join(", ", ids)})");

                continue;
            }

            file.PatchPath = path;
            file.PatchPassword = pathPassword;
            matched++;

            if (isDlc)
            {
                dlcMatched++;
                continue;
            }

            if (isUpdate)
                continue;

            if (note != null)
                details.AppendLine($"{name}: {note}");
            else
                details.AppendLine($"{name}: 프로그램 {match.Covered.Count}/{ids.Count}" + (match.Missing.Count > 0 ? $" | 일부 프로그램 패치 없음: {string.Join(", ", match.Missing)}" : string.Empty));
        }

        if (dlcTotal > 0)
            details.AppendLine($"DLC {dlcMatched}/{dlcTotal}");

        if (!source.HasIdDirs && !source.HasGeneric)
            details.AppendLine("[지정 실패] 타이틀 ID 폴더도 exefs/romfs/ips/xdelta도 찾지 못했습니다.");

        var unused = source.FoundIds.Where(id => !allIds.Contains(id)).ToList();

        if (unused.Count > 0)
            details.AppendLine($"[지정 경고] 불러온 게임과 무관한 패치 폴더 {unused.Count}개: {string.Join(", ", unused.Take(5))}{(unused.Count > 5 ? " ..." : string.Empty)}");

        return new BulkPatchReport(targets.Count, matched, details.ToString().TrimEnd());
    }
}