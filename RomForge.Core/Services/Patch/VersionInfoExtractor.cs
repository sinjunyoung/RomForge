using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public static partial class VersionInfoExtractor
{
    public const string DefaultNamingFormat = "{fileName} ({Lan1:code1}-{Lan2:code1}_v{Version}_{Date:yyMMdd})";

    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:v(?<Version>\d+(?:\.\d+)*[A-Za-z]*)|(?<Version>\d+(?:\.\d+)+[A-Za-z]*)(?:v)?)(?![A-Za-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"(?<!\d)(?:\d{4}|(\d{2}))(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\((?<Paren>[^()]*)\)|\[(?<Square>[^\[\]]*)\]")]
    private static partial Regex BracketGroupRegex();

    [GeneratedRegex(@"(?:\s*[,;/|+&]\s*|\s+)")]
    private static partial Regex LanguageSeparatorRegex();

    [GeneratedRegex(@"\{fileName\}", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameTokenRegex();

    [GeneratedRegex(@"[ _\-]?\{Version\}[ _\-]?", RegexOptions.IgnoreCase)]
    private static partial Regex VersionTokenRegex();

    [GeneratedRegex(@"\{Date(?::(?<Format>[^{}]+))?\}", RegexOptions.IgnoreCase)]
    private static partial Regex DateTokenRegex();

    [GeneratedRegex(@"[ _\-]?\{Lan(?:guage|1)(?::(?<Format>[^{}]+))?\}[ _\-]?", RegexOptions.IgnoreCase)]
    private static partial Regex Lan1TokenRegex();

    [GeneratedRegex(@"[ _\-]?\{Lan2(?::(?<Format>[^{}]+))?\}[ _\-]?", RegexOptions.IgnoreCase)]
    private static partial Regex Lan2TokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\{Lan(?:guage|1)(?::[^{}]+)?\}", RegexOptions.IgnoreCase)]
    private static partial Regex LanPlaceholderRegex();

    [GeneratedRegex(@"\{Lan2(?::[^{}]+)?\}", RegexOptions.IgnoreCase)]
    private static partial Regex Lan2PlaceholderRegex();

    [GeneratedRegex(@"\(\s*[\-_]\s*")]
    private static partial Regex ParenDashOpenRegex();

    [GeneratedRegex(@"\s*[\-_]\s*\)")]
    private static partial Regex ParenDashCloseRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex EmptyParenRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex EmptySquareBracketRegex();

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static readonly Dictionary<string, (string Code1, string Code2, string Code3, string Full)> LanguageMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["J"] = ("J", "Ja", "Jap", "Japan"),
            ["Ja"] = ("J", "Ja", "Jap", "Japan"),
            ["Jap"] = ("J", "Ja", "Jap", "Japan"),
            ["Japan"] = ("J", "Ja", "Jap", "Japan"),
            ["Japanese"] = ("J", "Ja", "Jap", "Japanese"),

            ["K"] = ("K", "Ko", "Kor", "Korea"),
            ["Korea"] = ("K", "Ko", "Kor", "Korea"),
            ["Korean"] = ("K", "Ko", "Kor", "Korean"),
            ["Ko"] = ("K", "Ko", "Kor", "Korean"),

            ["U"] = ("U", "En", "USA", "USA"),
            ["USA"] = ("U", "En", "USA", "USA"),
            ["Us"] = ("U", "En", "Usa", "English (US)"),

            ["E"] = ("E", "En", "Eur", "Europe"),
            ["Europe"] = ("E", "En", "Eur", "Europe"),
            ["En"] = ("E", "En", "Eng", "English"),
            ["English"] = ("E", "En", "Eng", "English"),

            ["C"] = ("C", "Zh", "Chn", "China"),
            ["China"] = ("C", "Zh", "Chn", "China"),
            ["Cn"] = ("C", "Zh", "Chn", "Chinese"),
            ["Zh"] = ("C", "Zh", "Chn", "Chinese"),
            ["Chinese"] = ("C", "Zh", "Chn", "Chinese"),
            ["Zh-Hans"] = ("C", "Zh", "Zhs", "Simplified Chinese"),
            ["Chs"] = ("C", "Zh", "Zhs", "Simplified Chinese"),
            ["SimplifiedChinese"] = ("C", "Zh", "Zhs", "Simplified Chinese"),
            ["Zh-Hant"] = ("T", "Zh", "Zht", "Traditional Chinese"),
            ["Cht"] = ("T", "Zh", "Zht", "Traditional Chinese"),
            ["TraditionalChinese"] = ("T", "Zh", "Zht", "Traditional Chinese"),

            ["W"] = ("W", "Wo", "Wld", "World"),
            ["World"] = ("W", "Wo", "Wld", "World"),

            ["A"] = ("A", "En", "Aus", "Australia"),
            ["Asia"] = ("A", "En", "Asi", "Asia"),
            ["Australia"] = ("A", "En", "Aus", "Australia"),
            ["Aus"] = ("A", "En", "Aus", "Australia"),
            ["Au"] = ("A", "En", "Aus", "English (Australia)"),
            ["Ar"] = ("A", "Ar", "Ara", "Arabic"),

            ["Taiwan"] = ("T", "Zh", "Twn", "Taiwan"),
            ["Tw"] = ("T", "Zh", "Twn", "Taiwanese"),

            ["F"] = ("F", "Fr", "Fra", "French"),
            ["Fr"] = ("F", "Fr", "Fra", "French"),
            ["French"] = ("F", "Fr", "Fra", "French"),

            ["D"] = ("D", "De", "Deu", "German"),
            ["De"] = ("D", "De", "Deu", "German"),
            ["German"] = ("D", "De", "Deu", "German"),

            ["S"] = ("S", "Es", "Spa", "Spanish"),
            ["Es"] = ("S", "Es", "Spa", "Spanish"),
            ["Spanish"] = ("S", "Es", "Spa", "Spanish"),

            ["I"] = ("I", "It", "Ita", "Italian"),
            ["It"] = ("I", "It", "Ita", "Italian"),
            ["Italian"] = ("I", "It", "Ita", "Italian"),

            ["Nl"] = ("N", "Nl", "Nld", "Dutch"),
            ["Dutch"] = ("N", "Nl", "Nld", "Dutch"),

            ["Pt"] = ("P", "Pt", "Por", "Portuguese"),
            ["Portuguese"] = ("P", "Pt", "Por", "Portuguese"),
            ["Pt-BR"] = ("P", "Pt", "Pbr", "Brazilian Portuguese"),

            ["Ru"] = ("R", "Ru", "Rus", "Russian"),
            ["Russian"] = ("R", "Ru", "Rus", "Russian"),

            ["Pl"] = ("P", "Pl", "Pol", "Polish"),
            ["Tr"] = ("T", "Tr", "Tur", "Turkish"),
            ["Es-MX"] = ("S", "Es", "Smx", "Latin American Spanish")
        };

    private static readonly Dictionary<string, (string Code1, string Code2, string Code3, string Full)> LanguageByFull = LanguageMap.Values
        .GroupBy(x => x.Full, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

    public static (string? Version, string? Date, string? Language) Extract(string patchFileName)
    {
        string name = Path.GetFileNameWithoutExtension(patchFileName);
        var versionMatch = VersionRegex().Match(name);
        var dateMatch = DateRegex().Match(name);
        string? version = versionMatch.Success ? versionMatch.Groups["Version"].Value : null;
        string? date = null;

        if (dateMatch.Success)
        {
            string rawDate = dateMatch.Value;

            date = rawDate.Length == 8 ? rawDate[2..] : rawDate;
        }

        return (version, date, ExtractLanguage(name));
    }

    public static string ApplySuffix(string fileName, string patchFilePath, bool namingEnabled = true, string? namingFormat = null, string? appLanguage = "Korean")
    {
        if (!namingEnabled)
            return fileName;

        var (version, date, language) = GetPatchMetadata(patchFilePath);

        return ApplyFormat(fileName, version, date, language, namingFormat, appLanguage);
    }

    public static string ApplyFormat(string fileName, string? version, string date, string? language, string? namingFormat, string? appLanguage = "Korean")
    {
        string rawNameOnly = Path.GetFileNameWithoutExtension(fileName);
        string? originalLanguage = ExtractLanguage(rawNameOnly);
        string patchLanguage = !string.IsNullOrWhiteSpace(language) ? NormalizeLanguage(language) : NormalizeLanguage(appLanguage);
        string nameOnly = RemoveLanguageGroups(rawNameOnly);

        nameOnly = WhitespaceRegex().Replace(nameOnly, " ").Trim();

        string ext = Path.GetExtension(fileName);
        string format = IsValidFormat(namingFormat) ? namingFormat! : DefaultNamingFormat;
        string result = FileNameTokenRegex().Replace(format, _ => nameOnly);

        result = VersionTokenRegex().Replace(result, m => version is null ? string.Empty : m.Value.Replace("{Version}", version, StringComparison.OrdinalIgnoreCase));
        result = Lan1TokenRegex().Replace(result, m =>
        {
            if (string.IsNullOrEmpty(originalLanguage))
                return string.Empty;

            string fmt = m.Groups["Format"].Success ? m.Groups["Format"].Value : "full";
            string formatted = FormatLanguage(originalLanguage, fmt);

            return LanPlaceholderRegex().Replace(m.Value, _ => formatted);
        });
        result = Lan2TokenRegex().Replace(result, m =>
        {
            if (string.IsNullOrEmpty(patchLanguage))
                return string.Empty;

            string fmt = m.Groups["Format"].Success ? m.Groups["Format"].Value : "full";
            string formatted = FormatLanguage(patchLanguage, fmt);

            return Lan2PlaceholderRegex().Replace(m.Value, _ => formatted);
        });
        result = DateTokenRegex().Replace(result, m =>
        {
            string dateFormat = m.Groups["Format"].Success ? m.Groups["Format"].Value : "yyMMdd";

            if (date.Length == 6 && DateTime.TryParseExact(date, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                return parsedDate.ToString(dateFormat);

            if (date.Length == 8 && DateTime.TryParseExact(date, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out parsedDate))
                return parsedDate.ToString(dateFormat);

            return date;
        });
        result = ParenDashOpenRegex().Replace(result, "(");
        result = ParenDashCloseRegex().Replace(result, ")");
        result = EmptyParenRegex().Replace(result, string.Empty);
        result = EmptySquareBracketRegex().Replace(result, string.Empty);
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return result + ext;
    }

    private static string? ExtractLanguage(string name)
    {
        foreach (Match group in BracketGroupRegex().Matches(name))
        {
            string content = GetBracketContent(group);
            string? language = ExtractLanguageFromGroup(content);

            if (language is not null)
                return language;
        }

        return null;
    }

    private static string? ExtractLanguageFromGroup(string content)
    {
        foreach (string part in LanguageSeparatorRegex().Split(content))
        {
            string candidate = part.Trim();

            if (LanguageMap.TryGetValue(candidate, out var mapped))
                return mapped.Full;

            if (LanguageByFull.TryGetValue(candidate, out mapped))
                return mapped.Full;
        }

        return null;
    }

    private static string GetBracketContent(Match match) => match.Groups["Paren"].Success ? match.Groups["Paren"].Value : match.Groups["Square"].Value;

    private static string NormalizeLanguage(string? rawLanguage)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return string.Empty;

        string value = rawLanguage.Trim();

        if (LanguageMap.TryGetValue(value, out var mapped))
            return mapped.Full;

        if (LanguageByFull.TryGetValue(value, out mapped))
            return mapped.Full;

        return value;
    }

    private static string RemoveLanguageGroups(string name)
    {
        return BracketGroupRegex().Replace(name, match =>
        {
            string content = GetBracketContent(match);
            return ExtractLanguageFromGroup(content) is not null ? string.Empty : match.Value;
        }).Trim();
    }

    private static string FormatLanguage(string rawLanguage, string formatType)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return string.Empty;

        string normalizedLanguage = NormalizeLanguage(rawLanguage);

        if (LanguageByFull.TryGetValue(normalizedLanguage, out var mapped))
        {
            return formatType.ToLowerInvariant() switch
            {
                "code1" or "1" => mapped.Code1,
                "code2" or "2" => mapped.Code2,
                "code3" or "3" => mapped.Code3,
                _ => mapped.Full
            };
        }

        return formatType.ToLowerInvariant() switch
        {
            "code1" or "1" => normalizedLanguage[..1].ToUpperInvariant(),
            "code2" or "2" => normalizedLanguage.Length >= 2 ? normalizedLanguage[..2] : normalizedLanguage,
            "code3" or "3" => normalizedLanguage.Length >= 3 ? normalizedLanguage[..3] : normalizedLanguage,
            _ => normalizedLanguage
        };
    }

    public static bool IsValidFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format) || !format.Contains("{fileName}", StringComparison.OrdinalIgnoreCase) || format.Length > 150)
            return false;

        string validationFormat = DateTokenRegex().Replace(format, "{Date}");

        validationFormat = Lan1TokenRegex().Replace(validationFormat, "{Lan1}");
        validationFormat = Lan2TokenRegex().Replace(validationFormat, "{Lan2}");

        return validationFormat.IndexOfAny(InvalidFileNameChars) < 0;
    }

    public static (string? Version, string Date, string? Language) GetPatchMetadata(string? patchFilePath)
    {
        string? version = null;
        string? date = null;
        string? language = null;

        if (!string.IsNullOrEmpty(patchFilePath) && File.Exists(patchFilePath))
        {
            var extracted = Extract(Path.GetFileName(patchFilePath));

            version = extracted.Version;
            date = extracted.Date;
            language = extracted.Language;
            date ??= File.GetLastWriteTime(patchFilePath).ToString("yyMMdd");
        }

        return (version, date ?? DateTime.Now.ToString("yyMMdd"), language);
    }
}