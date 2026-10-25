using Vita.Core.Services;

namespace Vita.Core.Models;

public sealed class PatchContext
{
    public required ISourceAccessor Accessor { get; init; }

    public required IReadOnlyList<PatchContainerFile> Files { get; init; }
}