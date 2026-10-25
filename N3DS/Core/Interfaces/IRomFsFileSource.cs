using Common;

namespace N3DS.Core.Interfaces;

public interface IRomFsFileSource
{
    ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, Action<string, LogLevel>? log = null, CancellationToken ct = default);
}