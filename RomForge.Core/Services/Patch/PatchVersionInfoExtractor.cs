using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public static class PatchVersionInfoExtractor
{
    public const string DefaultNamingFormat = "{fileName} ({Lan1:code1}-{Lan2:code1}_v{Version}_{Date:yyMMdd})";

    private static readonly Regex VersionRegex = new(@"(?<![A-Za-z0-9])(?:v(?<Version>\d+(?:\.\d+)*[A-Za-z]*)|(?<Version>\d+\.\d+[A-Za-z]*)(?:v)?)(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"(?<!\d)(?:\d{4}|(\d{2}))(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.Compiled);
    private static readonly Regex LanguageRegex = new(@"(?<![A-Za-z])(?<Language>Japan|USA|Europe|Asia|Korea|Korean|World|En|Ja|Ko|Zh|Fr|De|Es|It|J|K|U|E|C|A|W|F|D|S|I)(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileNameTokenRegex = new(@"\{fileName\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionTokenRegex = new(@"[ _\-]?\{Version\}[ _\-]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateTokenRegex = new(@"\{Date(?::(?<Format>[^{}]+))?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Lan1TokenRegex = new(@"[ _\-]?\{Lan(?:guage|1)(?::(?<Format>[^{}]+))?\}[ _\-]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Lan2TokenRegex = new(@"[ _\-]?\{Lan2(?::(?<Format>[^{}]+))?\}[ _\-]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private static readonly Dictionary<string, (string Code1, string Code3, string Full)> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["J"] = ("J", "Jap", "Japan"),
        ["K"] = ("K", "Kor", "Korea"),
        ["U"] = ("U", "USA", "USA"),
        ["E"] = ("E", "Eur", "Europe"),
        ["C"] = ("C", "Chn", "China"),
        ["W"] = ("W", "Wld", "World"),
        ["A"] = ("A", "Aus", "Australia"),
        ["F"] = ("F", "Fra", "French"),
        ["D"] = ("D", "Deu", "German"),
        ["S"] = ("S", "Spa", "Spanish"),
        ["I"] = ("I", "Ita", "Italian"),

        ["Japan"] = ("J", "Jap", "Japan"),
        ["USA"] = ("U", "USA", "USA"),
        ["Europe"] = ("E", "Eur", "Europe"),
        ["Korea"] = ("K", "Kor", "Korea"),
        ["Korean"] = ("K", "Kor", "Korea"),
        ["World"] = ("W", "Wld", "World"),
        ["China"] = ("C", "Chn", "China"),
        ["Taiwan"] = ("T", "Twn", "Taiwan"),
        ["Australia"] = ("A", "Aus", "Australia"),
        ["Aus"] = ("A", "Aus", "Australia"),

        ["Ko"] = ("K", "Kor", "Korean"),
        ["Ja"] = ("J", "Jap", "Japanese"),
        ["En"] = ("E", "Eng", "English"),
        ["Us"] = ("U", "Usa", "English (US)"),
        ["Au"] = ("A", "Aus", "English (Australia)"),

        ["Zh"] = ("C", "Chn", "Chinese"),
        ["Zh-Hans"] = ("C", "Zhs", "Simplified Chinese"),
        ["Zh-Hant"] = ("T", "Zht", "Traditional Chinese"),
        ["Chs"] = ("C", "Zhs", "Simplified Chinese"),
        ["Cht"] = ("T", "Zht", "Traditional Chinese"),
        ["Cn"] = ("C", "Chn", "Chinese"),
        ["Tw"] = ("T", "Twn", "Taiwanese"),

        ["Fr"] = ("F", "Fra", "French"),
        ["De"] = ("D", "Deu", "German"),
        ["Es"] = ("S", "Spa", "Spanish"),
        ["It"] = ("I", "Ita", "Italian"),
        ["Nl"] = ("N", "Nld", "Dutch"),
        ["Pt"] = ("P", "Por", "Portuguese"),
        ["Ru"] = ("R", "Rus", "Russian"),

        ["Pl"] = ("P", "Pol", "Polish"),
        ["Tr"] = ("T", "Tur", "Turkish"),
        ["Ar"] = ("A", "Ara", "Arabic"),
        ["Pt-BR"] = ("P", "Pbr", "Brazilian Portuguese"),
        ["Es-MX"] = ("S", "Smx", "Latin American Spanish"),

        ["Japanese"] = ("J", "Jap", "Japanese"),
        ["English"] = ("E", "Eng", "English"),
        ["Chinese"] = ("C", "Chn", "Chinese"),
        ["SimplifiedChinese"] = ("C", "Zhs", "Simplified Chinese"),
        ["TraditionalChinese"] = ("T", "Zht", "Traditional Chinese"),
        ["French"] = ("F", "Fra", "French"),
        ["German"] = ("D", "Deu", "German"),
        ["Spanish"] = ("S", "Spa", "Spanish"),
        ["Italian"] = ("I", "Ita", "Italian"),
        ["Dutch"] = ("N", "Nld", "Dutch"),
        ["Portuguese"] = ("P", "Por", "Portuguese"),
        ["Russian"] = ("R", "Rus", "Russian")
    };

    public static (string? Version, string? Date, string? Language) Extract(string patchFileName)
    {
        string name = Path.GetFileNameWithoutExtension(patchFileName);
        var versionMatch = VersionRegex.Match(name);
        var dateMatch = DateRegex.Match(name);
        var langMatch = LanguageRegex.Match(name);

        string? version = versionMatch.Success ? versionMatch.Groups["Version"].Value : null;
        string? language = langMatch.Success ? langMatch.Groups["Language"].Value : null;
        string? date = null;

        if (dateMatch.Success)
        {
            string rawDate = dateMatch.Value;
            date = rawDate.Length == 8 ? rawDate[2..] : rawDate;
        }

        return (version, date, language);
    }

    public static string ApplySuffix(string fileName, string patchFilePath, bool namingEnabled = true, string? namingFormat = null, string appLanguage = "Korean")
    {
        if (!namingEnabled)
            return fileName;

        var (version, date, language) = Extract(Path.GetFileName(patchFilePath));

        date ??= File.GetCreationTime(patchFilePath).ToString("yyMMdd");

        return ApplyFormat(fileName, version, date, language, namingFormat, appLanguage);
    }

    public static string ApplyFormat(string fileName, string? version, string date, string? language, string? namingFormat, string appLanguage = "Korean")
    {
        string rawNameOnly = Path.GetFileNameWithoutExtension(fileName);

        string? originalLanguage = null;
        var origLangMatch = LanguageRegex.Match(rawNameOnly);
        if (origLangMatch.Success)
        {
            originalLanguage = origLangMatch.Groups["Language"].Value;
        }

        string patchLanguage = !string.IsNullOrEmpty(language) ? language : appLanguage;

        string nameOnly = Regex.Replace(rawNameOnly, @"[\(\[]\s*(?:Japan|USA|Europe|Asia|Korea|Korean|World|En|Ja|Ko|Zh|Fr|De|Es|It|J|K|U|E|C|A|W|F|D|S|I)\s*[\)\]]", string.Empty, RegexOptions.IgnoreCase).Trim();
        nameOnly = Regex.Replace(nameOnly, @"\s+", " ");

        string ext = Path.GetExtension(fileName);
        string format = IsValidFormat(namingFormat) ? namingFormat! : DefaultNamingFormat;

        string result = FileNameTokenRegex.Replace(format, nameOnly);

        result = VersionTokenRegex.Replace(result, m => version is null ? string.Empty : Regex.Replace(m.Value, @"\{Version\}", version, RegexOptions.IgnoreCase));

        result = Lan1TokenRegex.Replace(result, m =>
        {
            if (string.IsNullOrEmpty(originalLanguage))
                return string.Empty;

            string fmt = m.Groups["Format"].Success ? m.Groups["Format"].Value : "full";
            string formatted = FormatLanguage(originalLanguage, fmt);

            return Regex.Replace(m.Value, @"\{Lan(?:guage|1)(?::[^{}]+)?\}", formatted, RegexOptions.IgnoreCase);
        });

        result = Lan2TokenRegex.Replace(result, m =>
        {
            if (string.IsNullOrEmpty(patchLanguage))
                return string.Empty;

            string fmt = m.Groups["Format"].Success ? m.Groups["Format"].Value : "full";
            string formatted = FormatLanguage(patchLanguage, fmt);

            return Regex.Replace(m.Value, @"\{Lan2(?::[^{}]+)?\}", formatted, RegexOptions.IgnoreCase);
        });

        result = DateTokenRegex.Replace(result, m =>
        {
            string dateFormat = m.Groups["Format"].Success ? m.Groups["Format"].Value : "yyMMdd";

            if (date.Length == 6 && DateTime.TryParseExact(date, "yyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                return parsedDate.ToString(dateFormat);

            if (date.Length == 8 && DateTime.TryParseExact(date, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out parsedDate))
                return parsedDate.ToString(dateFormat);

            return date;
        });

        result = Regex.Replace(result, @"\(\s*[\-_]\s*", "(");
        result = Regex.Replace(result, @"\s*[\-_]\s*\)", ")");
        result = Regex.Replace(result, @"\(\s*\)", string.Empty);
        result = Regex.Replace(result, @"\[\s*\]", string.Empty);
        result = Regex.Replace(result, @"\s+", " ").Trim();

        return result + ext;
    }

    private static string FormatLanguage(string rawLanguage, string formatType)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
            return string.Empty;

        if (LanguageMap.TryGetValue(rawLanguage, out var mapped))
        {
            return formatType.ToLowerInvariant() switch
            {
                "code1" or "1" => mapped.Code1,
                "code3" or "3" => mapped.Code3,
                "full" or _ => mapped.Full
            };
        }

        return formatType.ToLowerInvariant() switch
        {
            "code1" or "1" => rawLanguage[..1].ToUpperInvariant(),
            "code3" or "3" => rawLanguage.Length >= 3 ? rawLanguage[..3] : rawLanguage,
            _ => rawLanguage
        };
    }

    public static bool IsValidFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return false;

        if (!format.Contains("{fileName}", StringComparison.OrdinalIgnoreCase))
            return false;

        if (format.Length > 150)
            return false;

        string validationFormat = DateTokenRegex.Replace(format, "{Date}");
        validationFormat = Lan1TokenRegex.Replace(validationFormat, "{Lan1}");
        validationFormat = Lan2TokenRegex.Replace(validationFormat, "{Lan2}");

        if (validationFormat.IndexOfAny(InvalidFileNameChars) >= 0)
            return false;

        return true;
    }
}