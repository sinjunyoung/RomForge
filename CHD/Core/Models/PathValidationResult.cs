namespace CHD.Core.Models;

public class PathValidationResult
{
    public bool IsValid { get; set; }

    public bool HasKoreanInDirectory { get; set; }

    public bool HasKoreanInFilename { get; set; }

    public string? Message { get; set; }
}