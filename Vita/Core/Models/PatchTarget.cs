using Patch.Core.Services;

namespace Vita.Core.Models;

public sealed class PatchTarget
{
    public required string RelativePath { get; init; }

    public required PatchTargetKind Kind { get; init; }

    public PatchAppEntry? Source { get; init; }

    public PatchFileRef? OverwriteFile { get; init; }

    public IReadOnlyList<PlannedPatch> Patches { get; init; } = [];

    public required long EstimatedSize { get; init; }

    public bool IsStrict => Kind == PatchTargetKind.Overwrite || Patches.Any(p => p.Strict);
}