using Common.Localization;

using System.Xml.Linq;

namespace WiiGC.Core.Services.Wii;

public static class RiivolutionParser
{
    public static RiivolutionPatchSet Parse(string path, IReadOnlyDictionary<string, int>? choices = null)
    {
        string xmlPath = ResolveXml(path);
        string sdRoot = ResolveSdRoot(xmlPath);
        var root = XDocument.Load(xmlPath).Root;

        if (root == null || root.Name.LocalName != "wiidisc")
            throw new InvalidDataException(Loc.S.ErrNotRiivolutionXml);

        var warnings = new List<string>();
        var gameIds = new List<string>();

        foreach (var id in root.Elements("id"))
        {
            string? game = (string?)id.Attribute("game");

            if (!string.IsNullOrWhiteSpace(game))
                gameIds.Add(game);

            if (id.Attribute("region") != null)
                warnings.Add(Loc.S.WarnIdRegionNotChecked);
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
            throw new InvalidDataException(Loc.S.ErrPatchReferencedFilesMissing(missing.Count, string.Join(", ", missing.Take(5))));

        foreach (var (name, count) in unsupported)
            warnings.Add(Loc.S.WarnUnsupportedElementCount(name, count));

        return new RiivolutionPatchSet(xmlPath, sdRoot, gameIds, replacements, warnings);
    }

    private static string ResolveXml(string path)
    {
        if (File.Exists(path))
            return Path.GetFullPath(path);

        if (!Directory.Exists(path))
            throw new FileNotFoundException(Loc.S.ErrRiivolutionXmlOrFolderNotFound, path);

        string directory = Path.Combine(path, "riivolution");

        if (!Directory.Exists(directory))
            directory = path;

        string[] files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            throw new InvalidDataException(Loc.S.ErrRiivolutionXmlNotInFolder);

        if (files.Length > 1)
            throw new InvalidDataException(Loc.S.ErrMultipleXmlSpecifyOne(files.Length, string.Join(", ", files.Select(Path.GetFileName))));

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
                warnings.Add(Loc.S.WarnOptionChoiceOutOfRange(name, index));

                continue;
            }

            foreach (var reference in options[index - 1].Elements("patch"))
            {
                string? id = (string?)reference.Attribute("id");

                if (id == null || !patches.TryGetValue(id, out var patch))
                    warnings.Add(Loc.S.WarnOptionReferencesMissingPatch(name, id));
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
            warnings.Add(Loc.S.WarnFileEntryMissingDiscOrExternal);
            return;
        }

        if (element.Attribute("offset") != null || element.Attribute("length") != null)
        {
            warnings.Add(Loc.S.WarnPartialOverwriteUnsupported(disc));
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
            warnings.Add(Loc.S.WarnFolderEntryMissingDiscOrExternal);
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
            warnings.Add(Loc.S.WarnDuplicateDiscPathUsingLast(disc));

        replacements[disc] = file;
    }

    private static string ResolveExternal(string sdRoot, string patchRoot, string external)
    {
        string relative = (patchRoot + "/" + external).Replace('\\', '/').Trim('/');
        string full = Path.GetFullPath(Path.Combine(sdRoot, relative));
        string boundary = Path.GetFullPath(sdRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Loc.S.ErrXmlPathOutsideSdRoot(external));

        return full;
    }

    private static string NormalizeDisc(string disc)
    {
        string normalized = disc.Replace('\\', '/');

        return normalized.StartsWith('/') ? normalized : "/" + normalized;
    }
}