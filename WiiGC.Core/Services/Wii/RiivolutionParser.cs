using System.Xml.Linq;

namespace WiiGC.Core.Services.Wii;

public sealed record RiivolutionPatchSet(
    string XmlPath,
    string SdRoot,
    IReadOnlyList<string> GameIds,
    IReadOnlyDictionary<string, string> Replacements,
    IReadOnlyList<string> Warnings)
{
    public bool MatchesDisc(string discId) => GameIds.Count == 0 || GameIds.Any(id => discId.StartsWith(id, StringComparison.OrdinalIgnoreCase));
}

public static class RiivolutionParser
{
    public static RiivolutionPatchSet Parse(string path, IReadOnlyDictionary<string, int>? choices = null)
    {
        string xmlPath = ResolveXml(path);
        string sdRoot = ResolveSdRoot(xmlPath);
        var root = XDocument.Load(xmlPath).Root;

        if (root == null || root.Name.LocalName != "wiidisc")
            throw new InvalidDataException("Riivolution XML이 아닙니다.");

        var warnings = new List<string>();
        var gameIds = new List<string>();

        foreach (var id in root.Elements("id"))
        {
            string? game = (string?)id.Attribute("game");

            if (!string.IsNullOrWhiteSpace(game))
                gameIds.Add(game);

            if (id.Attribute("region") != null)
                warnings.Add("<id region> 조건은 검사하지 않습니다.");
        }

        var patches = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var patch in root.Elements("patch"))
        {
            string? id = (string?)patch.Attribute("id");

            if (!string.IsNullOrEmpty(id))
                patches.TryAdd(id, patch);
        }

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var patch in SelectPatches(root, patches, choices, warnings))
            ApplyPatch(patch, sdRoot, replacements, unsupported, missing, warnings);

        if (missing.Count > 0)
            throw new InvalidDataException($"패치가 가리키는 파일 {missing.Count}개를 찾을 수 없습니다: {string.Join(", ", missing.Take(5))}");

        foreach (var (name, count) in unsupported)
            warnings.Add($"<{name}> {count}개는 지원하지 않아 적용되지 않습니다.");

        return new RiivolutionPatchSet(xmlPath, sdRoot, gameIds, replacements, warnings);
    }

    private static string ResolveXml(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        if (!Directory.Exists(path))
            throw new FileNotFoundException("Riivolution XML 또는 폴더를 찾을 수 없습니다.", path);

        string directory = Path.Combine(path, "riivolution");

        if (!Directory.Exists(directory))
            directory = path;

        string[] files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            throw new InvalidDataException("폴더에서 Riivolution XML을 찾을 수 없습니다.");

        if (files.Length > 1)
            throw new InvalidDataException($"XML이 {files.Length}개 있습니다. 사용할 XML 파일을 직접 지정해 주세요: {string.Join(", ", files.Select(Path.GetFileName))}");

        return Path.GetFullPath(files[0]);
    }

    private static string ResolveSdRoot(string xmlPath)
    {
        string directory = Path.GetDirectoryName(xmlPath)!;

        if (string.Equals(Path.GetFileName(directory), "riivolution", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(directory) ?? directory;

        return directory;
    }

    private static List<XElement> SelectPatches(XElement root, Dictionary<string, XElement> patches, IReadOnlyDictionary<string, int>? choices, List<string> warnings)
    {
        var selected = new List<XElement>();

        foreach (var option in root.Elements("options").Descendants("option"))
        {
            string name = (string?)option.Attribute("name") ?? string.Empty;
            int index = int.TryParse((string?)option.Attribute("default"), out int parsed) ? parsed : 0;

            if (choices != null && choices.TryGetValue(name, out int overridden))
                index = overridden;

            var options = option.Elements("choice").ToList();

            if (index < 1)
                continue;

            if (index > options.Count)
            {
                warnings.Add($"옵션 '{name}'의 선택값 {index}이(가) 범위를 벗어나 건너뜁니다.");

                continue;
            }

            foreach (var reference in options[index - 1].Elements("patch"))
            {
                string? id = (string?)reference.Attribute("id");

                if (id == null || !patches.TryGetValue(id, out var patch))
                    warnings.Add($"옵션 '{name}'이(가) 존재하지 않는 패치 '{id}'를 참조합니다.");
                else if (!selected.Contains(patch))
                    selected.Add(patch);
            }
        }

        return selected;
    }

    private static void ApplyPatch(XElement patch, string sdRoot, Dictionary<string, string> replacements, SortedDictionary<string, int> unsupported, List<string> missing, List<string> warnings)
    {
        string patchRoot = (string?)patch.Attribute("root") ?? string.Empty;

        foreach (var element in patch.Elements())
        {
            string kind = element.Name.LocalName;

            if (kind == "file")
                ApplyFile(element, sdRoot, patchRoot, replacements, missing, warnings);
            else if (kind == "folder")
                ApplyFolder(element, sdRoot, patchRoot, replacements, missing, warnings);
            else
                unsupported[kind] = unsupported.GetValueOrDefault(kind) + 1;
        }
    }

    private static void ApplyFile(XElement element, string sdRoot, string patchRoot, Dictionary<string, string> replacements, List<string> missing, List<string> warnings)
    {
        string? disc = (string?)element.Attribute("disc");
        string? external = (string?)element.Attribute("external");

        if (string.IsNullOrEmpty(disc) || string.IsNullOrEmpty(external))
        {
            warnings.Add("disc 또는 external이 없는 <file> 항목을 건너뜁니다.");

            return;
        }

        if (element.Attribute("offset") != null || element.Attribute("length") != null)
        {
            warnings.Add($"일부 영역 덮어쓰기(offset/length)는 지원하지 않아 건너뜁니다: {disc}");

            return;
        }

        string file = ResolveExternal(sdRoot, patchRoot, external);

        if (!File.Exists(file))
        {
            missing.Add(external);

            return;
        }

        Add(replacements, warnings, NormalizeDisc(disc), file);
    }

    private static void ApplyFolder(XElement element, string sdRoot, string patchRoot, Dictionary<string, string> replacements, List<string> missing, List<string> warnings)
    {
        string? disc = (string?)element.Attribute("disc");
        string? external = (string?)element.Attribute("external");

        if (string.IsNullOrEmpty(disc) || string.IsNullOrEmpty(external))
        {
            warnings.Add("disc 또는 external이 없는 <folder> 항목을 건너뜁니다.");

            return;
        }

        string directory = ResolveExternal(sdRoot, patchRoot, external);

        if (!Directory.Exists(directory))
        {
            missing.Add(external);

            return;
        }

        bool recursive = !string.Equals((string?)element.Attribute("recursive"), "false", StringComparison.OrdinalIgnoreCase);
        string discRoot = NormalizeDisc(disc).TrimEnd('/');

        foreach (string file in Directory.EnumerateFiles(directory, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            Add(replacements, warnings, discRoot + "/" + Path.GetRelativePath(directory, file).Replace('\\', '/'), file);
    }

    private static void Add(Dictionary<string, string> replacements, List<string> warnings, string disc, string file)
    {
        if (replacements.TryGetValue(disc, out string? existing) && !string.Equals(existing, file, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"같은 디스크 경로가 중복되어 마지막 항목을 사용합니다: {disc}");

        replacements[disc] = file;
    }

    private static string ResolveExternal(string sdRoot, string patchRoot, string external)
    {
        string relative = (patchRoot + "/" + external).Replace('\\', '/').Trim('/');
        string full = Path.GetFullPath(Path.Combine(sdRoot, relative));
        string boundary = Path.GetFullPath(sdRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"XML이 SD 루트 밖의 경로를 가리킵니다: {external}");

        return full;
    }

    private static string NormalizeDisc(string disc)
    {
        string normalized = disc.Replace('\\', '/');

        return normalized.StartsWith('/') ? normalized : "/" + normalized;
    }
}