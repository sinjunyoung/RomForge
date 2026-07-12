using System.Text.RegularExpressions;

namespace NSW.Utils;

public static class NspNameBuilder
{
    private static readonly char[] _invalidChars = [.. Path.GetInvalidFileNameChars()
    .Concat([':', '*', '?', '"', '<', '>', '|', '\\', '/'])
    .Distinct()];

    public static string FileNameBuild(string suffix, string krName, string enName, string titleId, string displayVersion, uint titleVersion, int dlcCount, NswContainerFormat format = NswContainerFormat.Nsp)
    {
        string namePart = BuildNamePart(krName, enName);
        string cleanVersion = NormalizeVersion(displayVersion);
        bool hasUpdate = cleanVersion != "1.0.0";
        string tagPart = BuildTagPart(true, hasUpdate, dlcCount);
        string infoPart = $"[{titleId.ToUpper()}] {tagPart} [v{cleanVersion}] [v{titleVersion}]";
        string baseName = ComposeBaseName(namePart, infoPart);
        string finalSuffix = BuildFinalSuffix(suffix, format);

        return $"{baseName}_{finalSuffix}";
    }

    public static string FileNameBuild(string suffix, string krName, string enName, string titleId, string displayVersion, uint titleVersion, int dlcCount, bool hasBase, bool hasUpdate, NswContainerFormat format = NswContainerFormat.Nsp)
    {
        string namePart = BuildNamePart(krName, enName);
        string cleanVersion = NormalizeVersion(displayVersion);
        string tagPart = BuildTagPart(hasBase, hasUpdate, dlcCount);
        string infoPart = $"[{titleId.ToUpper()}] {tagPart} [v{cleanVersion}] [v{titleVersion}]";
        string baseName = ComposeBaseName(namePart, infoPart);
        string finalSuffix = BuildFinalSuffix(suffix, format);

        return $"{baseName}_{finalSuffix}";
    }

    public static string DisplayNameBuild(string enName, string titleId, string displayVersion, int dlcCount, NswContainerFormat format = NswContainerFormat.Nsp)
    {
        string namePart = BuildNamePart(string.Empty, enName);
        string cleanVersion = NormalizeVersion(displayVersion);
        bool hasUpdate = cleanVersion != "1.0.0";
        string tagPart = BuildTagPart(true, hasUpdate, dlcCount);
        string infoPart = $"[{titleId.ToUpper()}] {tagPart} [v{cleanVersion}]";
        string baseName = ComposeBaseName(namePart, infoPart);

        return $"{baseName}.{GetExtension(format)}";
    }

    public static string DisplayNameBuild(string enName, string titleId, string displayVersion, int dlcCount, bool hasBase, bool hasUpdate, NswContainerFormat format = NswContainerFormat.Nsp)
    {
        string namePart = BuildNamePart(string.Empty, enName);
        string cleanVersion = NormalizeVersion(displayVersion);
        string tagPart = BuildTagPart(hasBase, hasUpdate, dlcCount);
        string infoPart = $"[{titleId.ToUpper()}] {tagPart} [v{cleanVersion}]";
        string baseName = ComposeBaseName(namePart, infoPart);

        return $"{baseName}.{GetExtension(format)}";
    }

    public static string DisplayNameBuild(string enName, string titleId, string displayVersion)
    {
        string namePart = BuildNamePart(string.Empty, enName);
        string cleanVersion = NormalizeVersion(displayVersion);
        string infoPart = $"[{titleId.ToUpper()}] [v{cleanVersion}]";
        string baseName = ComposeBaseName(namePart, infoPart);

        return $"{baseName}.nsp";
    }

    public static string SplitFileNameBuild(string krName, string enName, string titleId, string displayVersion, string typeTag, NswContainerFormat format = NswContainerFormat.Nsp)
    {
        string namePart = BuildNamePart(krName, enName);
        string cleanVersion = typeTag != "DLC" ? $" [v{NormalizeVersion(displayVersion)}]" : string.Empty;
        string infoPart = $"[{titleId.ToUpper()}] ({typeTag}){cleanVersion}";
        string baseName = ComposeBaseName(namePart, infoPart);

        return $"{baseName}.{GetExtension(format)}";
    }

    public static string CompressDisplayNameBuild(string krName, string titleId, string displayVersion)
    {
        string namePart = BuildNamePart(krName, string.Empty);
        string cleanVersion = NormalizeVersion(displayVersion);
        string infoPart = $"[{titleId.ToUpper()}] [v{cleanVersion}]";

        return ComposeBaseName(namePart, infoPart);
    }

    private static string BuildNamePart(string krName, string enName)
    {
        krName = SafeFileName(krName);
        enName = SafeFileName(enName);

        if (string.IsNullOrWhiteSpace(krName))
            return enName.Trim();

        return string.IsNullOrWhiteSpace(enName) || string.Equals(krName, enName, StringComparison.OrdinalIgnoreCase)
            ? krName.Trim()
            : $"{krName} {enName}".Trim();
    }

    private static string BuildTagPart(bool hasBase, bool hasUpdate, int dlcCount)
    {
        var tags = new List<string>();

        if (hasBase)
            tags.Add("B");

        if (hasUpdate)
            tags.Add("U");

        if (dlcCount > 0)
            tags.Add($"{dlcCount}D");

        return tags.Count > 0 ? $"({string.Join("+", tags)})" : string.Empty;
    }

    private static string ComposeBaseName(string namePart, string infoPart)
        => Regex.Replace($"{namePart} {infoPart}", @"\s+", " ").Trim();

    private static string BuildFinalSuffix(string suffix, NswContainerFormat format)
    {
        string ext = GetExtension(format);

        return HasKnownExtension(suffix) ? suffix : $"{suffix}.{ext}";
    }

    private static bool HasKnownExtension(string suffix)
        => suffix.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase)
        || suffix.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase)
        || suffix.EndsWith(".xci", StringComparison.OrdinalIgnoreCase)
        || suffix.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

    private static string GetExtension(NswContainerFormat format) => format switch
    {
        NswContainerFormat.Nsp => "nsp",
        NswContainerFormat.Nsz => "nsz",
        NswContainerFormat.Xci => "xci",
        NswContainerFormat.Xcz => "xcz",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
    };

    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "1.0.0";

        version = version.Trim();

        if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            return version[1..].Trim();

        return version;
    }

    public static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        foreach (char c in _invalidChars)
            name = name.Replace(c.ToString(), string.Empty);

        return name.Trim();
    }
}