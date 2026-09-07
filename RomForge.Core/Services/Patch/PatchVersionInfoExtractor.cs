using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public static class PatchVersionInfoExtractor
{
    private static readonly Regex VersionRegex = new(@"(?<![A-Za-z0-9])v?(\d+(?:\.\d+)+[A-Za-z]*)(?:v)?(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"(?<!\d)(\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.Compiled);

    public static (string? Version, string? Date) Extract(string patchFileName)
    {
        string name = Path.GetFileNameWithoutExtension(patchFileName);
        var versionMatch = VersionRegex.Match(name);
        var dateMatch = DateRegex.Match(name);
        string? version = versionMatch.Success ? versionMatch.Groups[1].Value : null;
        string? date = dateMatch.Success ? dateMatch.Value : null;

        return (version, date);
    }

    public static string ApplySuffix(string fileName, string patchFilePath)
    {
        var (version, date) = Extract(Path.GetFileName(patchFilePath));

        date ??= File.GetCreationTime(patchFilePath).ToString("yyMMdd");

        string nameOnly = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string suffix = version is not null ? $" (v{version}_{date})" : $" (v{date})";

        return nameOnly + suffix + ext;
    }
}