using Common;
using N3DS.Core.Crypto;
using N3DS.Core.Interfaces;
using N3DS.Core.IO;
using N3DS.Core.Models;

namespace N3DS.Core.Services;

public class SdNcsdSource(IReadOnlyList<Contents> contents, KeyStore keyStore, SdCrypto sdCrypto, Action<string, LogLevel>? log = null) : INcsdSource
{
    public Action<string, LogLevel>? Log { get; init; } = log;

    public IReadOnlyList<Contents> Contents { get; } = contents;

    public ValueTask<(Stream ncchStream, long ncchSize)> OpenContentDecrypted(int contentIndex)
    {
        var chunk = Contents.FirstOrDefault(c => c.ContentIndex == contentIndex) ?? throw new InvalidOperationException($"Content index {contentIndex}를 찾을 수 없습니다.");
        string filePath = chunk.FilePath ?? throw new InvalidOperationException($"Content index {contentIndex}의 FilePath가 없습니다.");
        string sdPath = chunk.SdPath ?? throw new InvalidOperationException($"Content index {contentIndex}의 SdPath가 없습니다.");
        Stream? src = null;

        try
        {
            src = File.OpenRead(filePath);

            var decrypted = new SdDecryptStream(src, sdPath, sdCrypto);
            var ncchStream = new NcchDecryptionStream(decrypted, 0, keyStore);

            return ValueTask.FromResult(((Stream)ncchStream, chunk.ContentSize));

        }
        catch
        {
            src?.Dispose();
            throw;
        }
    }

    public async Task WriteContentAsync(int contentIndex, Stream output, long totalBytes, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        var (stream, size) = await OpenContentDecrypted(contentIndex);

        await using (stream)
            await stream.CopyToAsync(output, size, 0, progress, ct);
    }

    public async ValueTask<NcchHeader> GetNcchHeaderAsync(int contentIndex, CancellationToken ct)
    {
        var (stream, _) = await OpenContentDecrypted(contentIndex);

        await using (stream)
        {
            byte[] buf = new byte[0x200];

            await stream.ReadExactlyAsync(buf, ct);

            return NcchHeader.Parse(buf, 0);
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}