using Vita.Core.Services;

namespace Vita.Core.Models;

public sealed class OwnerContext
{
    public required SourceItem Item { get; init; }

    public required PfsFileTable Table { get; init; }

    public required WorkBinLicense License { get; init; }

    public required ISourceAccessor WorkBinAccessor { get; init; }

    public required string WorkBinRelativePath { get; init; }
}