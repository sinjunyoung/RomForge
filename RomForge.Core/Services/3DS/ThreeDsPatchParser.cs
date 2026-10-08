using System.IO;
using System.Text.RegularExpressions;
using RomForge.Core.Models._3DS;

namespace RomForge.Core.Services._3DS;

public static class ThreeDsPatchParser
{
    private static readonly Regex TitleIdRegex = new(@"00040000[0-9a-fA-F]{8}", RegexOptions.Compiled);
    private static readonly Regex ProductCodeRegex = new(@"CTR-[A-Z0-9]{4}", RegexOptions.Compiled);

    public static ThreeDsPatchSet Parse(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("패치 경로가 비어 있습니다.", nameof(path));

        if (File.Exists(path))
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            return ext switch
            {
                ".zip" => ParseArchive(path, ThreeDsPatchKind.Zip, ct),
                ".7z" => ParseArchive(path, ThreeDsPatchKind.SevenZip, ct),
                _ => throw new InvalidDataException("지원하지 않는 패치 파일 형식입니다 (zip, 7z만 지원).")
            };
        }

        if (Directory.Exists(path))
            return ParseFolder(path, ct);

        throw new FileNotFoundException("패치 파일 또는 폴더를 찾을 수 없습니다.", path);
    }

    private static ThreeDsPatchSet ParseFolder(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(path, f).Replace('\\', '/'))
            .ToList();

        var targetIds = ExtractTargetIdsFromStructure(files);

        return new ThreeDsPatchSet(path, ThreeDsPatchKind.Folder, targetIds, files);
    }

    private static ThreeDsPatchSet ParseArchive(string path, ThreeDsPatchKind kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var files = new List<string>();

        using (var archive = SharpCompress.Archives.ArchiveFactory.OpenArchive(path))
        {
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (!entry.IsDirectory && !string.IsNullOrEmpty(entry.Key))
                    files.Add(entry.Key.Replace('\\', '/'));
            }
        }

        var targetIds = ExtractTargetIdsFromStructure(files);

        return new ThreeDsPatchSet(path, kind, targetIds, files);
    }

    private static List<string> ExtractTargetIdsFromStructure(List<string> files)
    {
        var targetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            string[] segments = file.Split('/');

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];

                if (IsPatchContentDirectory(segment))
                    break;

                Match titleMatch = TitleIdRegex.Match(segment);

                if (titleMatch.Success)
                {
                    targetIds.Add(titleMatch.Value.ToUpperInvariant());
                    continue;
                }

                Match productMatch = ProductCodeRegex.Match(segment);

                if (productMatch.Success)
                    targetIds.Add(productMatch.Value.ToUpperInvariant());
            }
        }

        return [.. targetIds];
    }

    private static bool IsPatchContentDirectory(string name)
        => name.Equals("romfs", StringComparison.OrdinalIgnoreCase) || name.Equals("exefs", StringComparison.OrdinalIgnoreCase) || name.Equals("echeader", StringComparison.OrdinalIgnoreCase)
        || name.Equals("icon", StringComparison.OrdinalIgnoreCase) || name.Equals("banner", StringComparison.OrdinalIgnoreCase);
}