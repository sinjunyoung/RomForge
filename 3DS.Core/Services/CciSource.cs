using _3DS.Core.Crypto;
using _3DS.Core.IO;
using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using Common;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace _3DS.Core.Services;

public class CciSource : IInstallSource
{
    private const int MediaUnit = 0x200;

    public required Stream Stream { get; init; }
    public required KeyStore KeyStore { get; init; }

    public required NcchHeader MainHeader { get; init; }

    public required (uint offset, uint size)[] PartitionMap { get; init; }

    public required IReadOnlyList<Contents> Contents { get; init; }

    public required TmdHeader TmdHeader { get; init; }

    public required byte[] TmdRaw { get; init; }

    public Action<string, LogLevel>? Log { get; init; }

    public static async Task<CciSource> OpenAsync(string path, KeyStore keyStore, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            byte[] magic = new byte[4];

            await stream.ReadExactlyAsync(magic, ct);

            stream.Position = 0;

            if (magic.AsSpan().SequenceEqual("Z3DS"u8))
            {
                var z3dsHeader = Z3dsFormat.ParseZ3dsHeader(stream);
                stream = new ZcciDecompressStream(stream, z3dsHeader);
            }

            byte[] ncsdBuf = new byte[0x200];

            stream.Position = 0x100;

            await stream.ReadExactlyAsync(ncsdBuf, ct);

            if (!ncsdBuf.AsSpan(0, 4).SequenceEqual("NCSD"u8))
                throw new InvalidDataException("NCSD 매직 불일치");

            var partitionMap = new (uint offset, uint size)[8];

            for (int i = 0; i < 8; i++)
            {
                int off = 0x20 + i * 8;
                uint pOffset = BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(off));
                uint pSize = BinaryPrimitives.ReadUInt32LittleEndian(ncsdBuf.AsSpan(off + 4));

                partitionMap[i] = (pOffset, pSize);
            }

            var contentsList = new List<Contents>();
            NcchHeader? mainHeader = null;
            uint saveSize = 0;

            for (int i = 0; i < 8; i++)
            {
                var (partOffset, partSize) = partitionMap[i];

                if (partOffset == 0 || partSize == 0)
                    continue;

                long byteOffset = (long)partOffset * MediaUnit;
                byte[] ncchBuf = new byte[0x200];

                stream.Position = byteOffset;

                await stream.ReadExactlyAsync(ncchBuf, ct);

                var ncch = NcchHeader.Parse(ncchBuf, 0);

                if (i == 0)
                {
                    mainHeader = ncch;

                    if (ncch.ExtendedHeaderSize > 0)
                    {
                        try
                        {
                            using var ncchStream = new NcchDecryptionStream(new SubStream(stream, byteOffset, (long)partSize * MediaUnit), 0, keyStore);
                            byte[] exheader = new byte[0x400];
                            ncchStream.Position = 0x200;
                            ncchStream.Read(exheader, 0, 0x400);
                            saveSize = BinaryPrimitives.ReadUInt32LittleEndian(exheader.AsSpan(0x1C0));
                        }
                        catch { }
                    }
                }

                contentsList.Add(new Contents
                {
                    ContentId = (uint)i,
                    ContentIndex = (ushort)i,
                    ContentSize = (long)partSize * MediaUnit,
                    ContentType = 0,
                });
            }

            if (mainHeader is null)
                throw new InvalidDataException("파티션 0을 찾을 수 없습니다.");

            var tmdHeader = new TmdHeader
            {
                TitleId = mainHeader.ProgramId,
                TitleVersion = mainHeader.Version,
                SaveSize = saveSize,
                Contents = [.. contentsList],
            };

            var emptyHashes = contentsList.Select(_ => new byte[0x20]).ToArray();
            byte[] tmdRaw = BuildTmdRaw(tmdHeader, contentsList, emptyHashes);

            return new CciSource
            {
                Stream = stream,
                KeyStore = keyStore,
                MainHeader = mainHeader,
                PartitionMap = partitionMap,
                Contents = contentsList,
                TmdHeader = tmdHeader,
                TmdRaw = tmdRaw,
                Log = log,
            };
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public ValueTask<(Stream ncchStream, long ncchSize)> OpenContentDecrypted(int contentIndex)
    {
        var (partOffset, partSize) = PartitionMap[contentIndex];
        long byteOffset = (long)partOffset * MediaUnit;
        long byteSize = (long)partSize * MediaUnit;

        return ValueTask.FromResult(((Stream)new NcchDecryptionStream(new SubStream(Stream, byteOffset, byteSize), 0, KeyStore), byteSize));
    }

    public ValueTask<(Stream stream, long size)> OpenContentNcchEncrypted(int contentIndex) => OpenContentDecrypted(contentIndex);

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

    private static byte[] BuildTmdRaw(TmdHeader tmd, List<Contents> contents, byte[][] hashes)
    {
        const int TmdBaseSize = 0x140 + 0xC4 + (0x24 * 64);
        const int TmdChunkSize = 0x30;
        const uint SigType = 0x00010004;
        int count = contents.Count;
        byte[] buf = new byte[TmdBaseSize + TmdChunkSize * count];

        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(0x00), SigType);

        int h = 0x140;

        "Root-CA00000004-CP0000000a"u8.CopyTo(buf.AsSpan(h));

        buf[h + 0x40] = 0x01;

        BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(h + 0x4C), tmd.TitleId);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(h + 0x54), 0x00000040);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(h + 0x5A), tmd.SaveSize);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(h + 0x9C), tmd.TitleVersion);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(h + 0x9E), (ushort)count);

        int infoOff = h + 0xC4;
        int chunkOff = infoOff + 0x24 * 64;

        for (int i = 0; i < count; i++)
        {
            var c = contents[i];
            int off = chunkOff + i * TmdChunkSize;
            BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(off + 0x00), (uint)c.ContentIndex);
            BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(off + 0x04), c.ContentIndex);
            BinaryPrimitives.WriteUInt64BigEndian(buf.AsSpan(off + 0x08), (ulong)c.ContentSize);
            hashes[i].CopyTo(buf, off + 0x10);
        }

        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff), 0);
        BinaryPrimitives.WriteUInt16BigEndian(buf.AsSpan(infoOff + 0x02), (ushort)count);
        SHA256.HashData(buf.AsSpan(chunkOff, TmdChunkSize * count)).CopyTo(buf, infoOff + 0x04);
        SHA256.HashData(buf.AsSpan(infoOff, 0x24 * 64)).CopyTo(buf, h + 0xA4);

        return buf;
    }

    public async ValueTask DisposeAsync() => await Stream.DisposeAsync();
}