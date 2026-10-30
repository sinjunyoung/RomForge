using CHD.Core.Models;
using System.Text.RegularExpressions;

namespace CHD.Core.Services;

public partial class FilenameHandler
{
    [GeneratedRegex(@"[\uAC00-\uD7A3]")]
    private static partial Regex KoreanRegex();

    public static bool HasKorean(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
            return false;

        return KoreanRegex().IsMatch(filename);
    }

    public static bool HasKoreanInDirectory(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string? directory = Path.GetDirectoryName(filePath);

        return !string.IsNullOrEmpty(directory) && HasKorean(directory);
    }

    public static bool HasKoreanInFilename(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string filename = Path.GetFileName(filePath);

        return HasKorean(filename);
    }

    public static PathValidationResult ValidatePath(string? filePath)
    {
        return new PathValidationResult
        {
            IsValid = true,
            HasKoreanInDirectory = HasKoreanInDirectory(filePath),
            HasKoreanInFilename = HasKoreanInFilename(filePath)
        };
    }
}