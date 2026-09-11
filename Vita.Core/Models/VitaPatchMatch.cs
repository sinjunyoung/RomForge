namespace Vita.Core.Models;

public sealed class VitaPatchMatch
{
    public required string SourceFile { get; init; }

    public required string PatchFile { get; init; }
}