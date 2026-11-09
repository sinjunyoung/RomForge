using Common.Models;
using LibHac.Common.Keys;

namespace NSW.HacPack.Models;

public sealed record NczOptions(KeySet KeySet, int CompressionLevel, SwitchCompressionMode CompressionMode, IProgress<ProgressInfo>? Progress, Action<long>? OnRead, CancellationToken Cancellation);