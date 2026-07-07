using _3DS.Core.Crypto;
using _3DS.Core.FileSystem;
using _3DS.Core.Interfaces;
using _3DS.Core.IO;
using _3DS.Core.Models;
using Common;
using System.Buffers.Binary;

namespace _3DS.Core.Services;

public class CiaSource : IInstallSource
{
    public required Stream Stream { get; init; }

    public required CiaHeader CiaHeader { get; init; }

    public required CiaTicket Ticket { get; init; }

    public required byte[] TitleKey { get; init; }

    public required TmdHeader TmdHeader { get; init; }

    public required KeyStore KeyStore { get; init; }

    public required long ContentOffset { get; init; }

    public required byte[] TmdRaw { get; init; }

    public required byte[] TicketRaw { get; init; }

    public SmdhInfo? SmdhInfo { get; set; }

    public Action<string, LogLevel, string>? Log { get; init; }

    public NcchHeader? MainNcchHeader { get; private set; }

    public IReadOnlyList<Contents> Contents => TmdHeader.Contents;

    public ValueTask<(Stream ncchStream, long ncchSize)> OpenContentDecrypted(int contentIndex) => OpenContent(contentIndex, null);

    public ValueTask<(Stream ncchStream, long ncchSize)> OpenContent(int contentIndex, Action<string, LogLevel, string>? log)
    {
        long offset = ContentOffset;
        Contents? target = null;

        foreach (var chunk in Contents)
        {
            if (!IsContentPresent(chunk.ContentIndex))
                continue;

            if (chunk.ContentIndex == contentIndex)
            {
                target = chunk;
                break;
            }

            offset = AlignUp(offset + chunk.ContentSize, 64);
        }

        if (target is null)
            throw new InvalidOperationException($"Content index {contentIndex}를 찾을 수 없습니다.");

        Stream rawStream;

        if (target.IsEncrypted)
        {
            log?.Invoke($"[Content {contentIndex}] 암호화된 콘텐츠 감지, 복호화 파이프라인 구동...", LogLevel.Info, string.Empty);

            byte[] iv = new byte[16];
            iv[0] = (byte)(target.ContentIndex >> 8);
            iv[1] = (byte)(target.ContentIndex & 0xFF);

            rawStream = new AesCbcSubStream(Stream, offset, target.ContentSize, TitleKey, iv);
        }
        else
            rawStream = new SubStream(Stream, offset, target.ContentSize);

        var ncchStream = new NcchDecryptionStream(rawStream, 0, KeyStore);

        return ValueTask.FromResult(((Stream)ncchStream, target.ContentSize));
    }

    public ValueTask<(Stream stream, long size)> OpenContentNcchEncrypted(int contentIndex)
    {
        long offset = ContentOffset;
        Contents? target = null;

        foreach (var chunk in Contents)
        {
            if (!IsContentPresent(chunk.ContentIndex))
                continue;

            if (chunk.ContentIndex == contentIndex)
            {
                target = chunk;
                break;
            }

            offset = AlignUp(offset + chunk.ContentSize, 64);
        }

        if (target is null)
            throw new InvalidOperationException($"Content index {contentIndex}를 찾을 수 없습니다.");

        Stream stream = target.IsEncrypted
            ? new AesCbcSubStream(Stream, offset, target.ContentSize, TitleKey, new byte[] { (byte)(target.ContentIndex >> 8), (byte)(target.ContentIndex & 0xFF), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })
            : new SubStream(Stream, offset, target.ContentSize);

        return ValueTask.FromResult((stream, target.ContentSize));
    }

    public async Task WriteContentAsync(int contentIndex, Stream output, long totalBytes, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        var (stream, size) = await OpenContentDecrypted(contentIndex);

        await using (stream)
            await stream.CopyToAsync(output, size, 0, progress, ct);
    }

    public async ValueTask<NcchHeader> GetNcchHeaderAsync(int contentIndex, CancellationToken ct = default)
    {
        var (stream, _) = await OpenContentDecrypted(contentIndex);

        await using (stream)
        {
            byte[] buf = new byte[0x200];

            await stream.ReadExactlyAsync(buf, ct);

            return NcchHeader.Parse(buf, 0);
        }
    }

    public async Task LoadGameInfoAsync(int contentIndex = 0)
    {
        var (ncchStream, _) = await OpenContentDecrypted(contentIndex);
        byte[] ncchHeaderBuf = new byte[0x200];

        await ncchStream.ReadExactlyAsync(ncchHeaderBuf, 0, 0x200);

        MainNcchHeader = NcchHeader.Parse(ncchHeaderBuf, 0);
        long exefsOffset = (long)MainNcchHeader.ExefsOffset * 0x200;

        ncchStream.Position = exefsOffset;

        byte[] headerBuffer = new byte[0x200];

        await ncchStream.ReadExactlyAsync(headerBuffer, 0, 0x200);

        for (int i = 0; i < 8; i++)
        {
            int entryBase = i * 16;
            string name = System.Text.Encoding.ASCII.GetString(headerBuffer, entryBase, 8).TrimEnd('\0');

            if (name == "icon")
            {
                uint fileOffset = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(entryBase + 8, 4));
                uint fileSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(entryBase + 12, 4));
                byte[] smdhData = new byte[fileSize];

                ncchStream.Position = exefsOffset + 0x200 + fileOffset;
                await ncchStream.ReadExactlyAsync(smdhData, 0, (int)fileSize);                

                SmdhInfo = SmdhParser.TryParse(smdhData);

                if (SmdhInfo != null)
                    return;
            }
        }
    }

    public bool IsContentPresent(int contentIndex) => CiaHeader.IsContentPresent(contentIndex);

    private static long AlignUp(long value, int alignment) => (value + alignment - 1) & ~(alignment - 1L);

    public async ValueTask DisposeAsync() => await Stream.DisposeAsync();
}