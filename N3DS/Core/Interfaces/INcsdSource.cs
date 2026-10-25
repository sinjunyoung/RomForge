using Common;
using N3DS.Core.Models;

namespace N3DS.Core.Interfaces;

public interface INcsdSource : IAsyncDisposable
{
    IReadOnlyList<Contents> Contents { get; }

    ValueTask<(Stream ncchStream, long ncchSize)> OpenContentDecrypted(int contentIndex);

    Task WriteContentAsync(int contentIndex, Stream output, long totalBytes, Action<long, long>? progress = null, CancellationToken ct = default);

    ValueTask<NcchHeader> GetNcchHeaderAsync(int contentIndex, CancellationToken ct = default);

    Action<string, LogLevel>? Log { get; init; }

    bool IsContentPresent(int contentIndex) => true;
}