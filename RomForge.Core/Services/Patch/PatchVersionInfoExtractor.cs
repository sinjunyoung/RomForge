using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public static class PatchVersionInfoExtractor
{
    public const string DefaultNamingFormat = "{fileName} (v{Version}_{Date})";

    private static readonly Regex VersionRegex = new(@"(?<![A-Za-z0-9])v?(\d+(?:\.\d+)+[A-Za-z]*)(?:v)?(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);    
    private static readonly Regex DateRegex = new(@"(?<!\d)(?:\d{4}|(\d{2}))(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.Compiled);
    private static readonly Regex FileNameTokenRegex = new(@"\{fileName\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionTokenRegex = new(@"[ _\-]?\{Version\}[ _\-]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateTokenRegex = new(@"\{Date\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static (string? Version, string? Date) Extract(string patchFileName)
    {
        string name = Path.GetFileNameWithoutExtension(patchFileName);
        var versionMatch = VersionRegex.Match(name);
        var dateMatch = DateRegex.Match(name);
        string? version = versionMatch.Success ? versionMatch.Groups[1].Value : null;
        string? date = null;

        if (dateMatch.Success)
        {
            string rawDate = dateMatch.Value;
            date = rawDate.Length == 8 ? rawDate[2..] : rawDate;
        }

        return (version, date);
    }

    public static string ApplySuffix(string fileName, string patchFilePath, bool namingEnabled = true, string? namingFormat = null)
    {
        if (!namingEnabled)
            return fileName;

        var (version, date) = Extract(Path.GetFileName(patchFilePath));

        date ??= File.GetCreationTime(patchFilePath).ToString("yyMMdd");

        return ApplyFormat(fileName, version, date, namingFormat);
    }

    public static string ApplyFormat(string fileName, string? version, string date, string? namingFormat)
    {
        string nameOnly = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string format = IsValidFormat(namingFormat) ? namingFormat! : DefaultNamingFormat;
        string result = FileNameTokenRegex.Replace(format, nameOnly);

        result = VersionTokenRegex.Replace(result, m => version is null ? "" : Regex.Replace(m.Value, @"\{Version\}", version, RegexOptions.IgnoreCase));
        result = DateTokenRegex.Replace(result, date);

        return result + ext;
    }

    public static bool IsValidFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return false;

        if (!format.Contains("{fileName}", StringComparison.OrdinalIgnoreCase))
            return false;

        if (format.Length > 150)
            return false;

        if (format.IndexOfAny(InvalidFileNameChars) >= 0)
            return false;

        return true;
    }
}