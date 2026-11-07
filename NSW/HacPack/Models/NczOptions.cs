using Common.Models;
using LibHac.Common.Keys;

namespace NSW.HacPack.Models;

public sealed record NczOptions(KeySet KeySet, int CompressionLevel, bool UseBlockMode, IProgress<ProgressInfo>? Progress, Action<long>? OnRead, CancellationToken Cancellation);