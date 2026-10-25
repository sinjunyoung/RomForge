namespace CHD.Core.Models;

public class ConversionResult
{
    public bool Success { get; set; }

    public string Message { get; set; }

    public string OutputFile { get; set; }

    public IReadOnlyList<string> OutputFiles { get; init; } = [];

    public bool VerificationPerformed { get; set; } = false;

    public static ConversionResult Fail(string message) =>
        new()
        {
            Success = false,
            Message = message,
            OutputFile = null
        };
}