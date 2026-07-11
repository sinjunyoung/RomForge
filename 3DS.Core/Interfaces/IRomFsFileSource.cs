namespace _3DS.Core.Interfaces;

public interface IRomFsFileSource
{
    ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, CancellationToken ct = default);
}