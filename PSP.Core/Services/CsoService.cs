using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Models.Enums;
using CHD.Core.Services;
using Common;
using K4os.Compression.LZ4;
using LibDeflate;
using PSP.Core.Models;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;

namespace PSP.Core.Services;

public class CsoService
{
    private const uint HeaderSize = 0x18;

    private readonly ChdmanService _chdman = new();

    public static async Task DecompressAsync(Stream input, Stream output, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var magic = new byte[4];

        await input.ReadExactlyAsync(magic, ct);

        bool isZso = magic.SequenceEqual(CsoHeader.MagicZSO);

        if (!isZso && !magic.SequenceEqual(CsoHeader.MagicCSO))
            throw new InvalidDataException("CSO/ZSO 매직 불일치");

        var headerBytes = new byte[HeaderSize - 4];

        await input.ReadExactlyAsync(headerBytes, ct);

        var header = new CsoHeader
        {
            HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan()[0..]),
            UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan()[4..]),
            BlockSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan()[12..]),
            Version = headerBytes[16],
            IndexShift = headerBytes[17],
        };

        int blockCount = (int)Math.Ceiling((double)header.UncompressedSize / header.BlockSize);
        var indexTable = new uint[blockCount + 1];
        var indexBytes = new byte[(blockCount + 1) * 4];

        await input.ReadExactlyAsync(indexBytes, ct);

        for (int i = 0; i <= blockCount; i++)
            indexTable[i] = BinaryPrimitives.ReadUInt32LittleEndian(indexBytes.AsSpan(i * 4));

        var blockBuf = new byte[header.BlockSize * 2];
        var reporter = progress is null ? null : new ProgressReporter("압축 해제 중...", string.Empty, blockCount, progress);
        var report = reporter?.CreateAction();

        for (int i = 0; i < blockCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            uint entry = indexTable[i];
            uint nextEntry = indexTable[i + 1];
            bool uncompressed = (entry & 0x80000000u) != 0;
            long offset = (long)(entry & 0x7FFFFFFFu) << header.IndexShift;
            long nextOffset = (long)(nextEntry & 0x7FFFFFFFu) << header.IndexShift;
            int blockLen = (int)(nextOffset - offset);

            input.Seek(offset, SeekOrigin.Begin);

            var compressed = new byte[blockLen];

            await input.ReadExactlyAsync(compressed, ct);

            if (uncompressed)
                await output.WriteAsync(compressed, ct);
            else if (header.Version == 2 || isZso)
            {
                int decoded = LZ4Codec.Decode(compressed, 0, blockLen, blockBuf, 0, (int)header.BlockSize);

                await output.WriteAsync(blockBuf.AsMemory(0, decoded), ct);
            }
            else
            {
                using var ms = new MemoryStream(compressed);
                using var ds = new DeflateStream(ms, CompressionMode.Decompress);

                await ds.CopyToAsync(output, ct);
            }

            report?.Invoke(i + 1, blockCount);
        }
    }

    public static async Task CompressAsync(Stream input, Stream output, byte[]? magic = null, byte version = 1, bool isLz4 = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        magic ??= CsoHeader.MagicCSO;
        isLz4 = isLz4 || version == 2;

        await output.WriteAsync(magic, ct);

        const uint blockSize = 2048;
        long totalSize = input.Length;
        int blockCount = (int)Math.Ceiling((double)totalSize / blockSize);
        var headerBytes = new byte[HeaderSize - 4];

        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.AsSpan()[0..], HeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(headerBytes.AsSpan()[4..], (ulong)totalSize);
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.AsSpan()[12..], blockSize);

        headerBytes[16] = version;
        headerBytes[17] = 0;

        await output.WriteAsync(headerBytes, ct);

        long indexOffset = output.Position;
        var indexTable = new uint[blockCount + 1];

        await output.WriteAsync(new byte[(blockCount + 1) * 4], ct);

        var inputBuf = new byte[blockSize];
        var compBuf = new byte[blockSize * 2];
        var reporter = progress is null ? null : new ProgressReporter("압축 중...", string.Empty, blockCount, progress);
        var report = reporter?.CreateAction();

        for (int i = 0; i < blockCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            int read = await input.ReadAtLeastAsync(inputBuf, (int)Math.Min(blockSize, totalSize - (long)i * blockSize), throwOnEndOfStream: false, ct);
            long blockOffset = output.Position;

            indexTable[i] = (uint)blockOffset;

            int compLen;
            bool useUncompressed;

            if (isLz4)
            {
                compLen = LZ4Codec.Encode(inputBuf, 0, read, compBuf, 0, compBuf.Length);
                useUncompressed = compLen <= 0 || compLen >= read;
            }
            else
            {
                using var compressor = new DeflateCompressor(1);
                compLen = compressor.Compress(inputBuf.AsSpan(0, read), compBuf);
                useUncompressed = compLen <= 0 || compLen >= read;
            }

            if (useUncompressed)
            {
                indexTable[i] |= 0x80000000u;
                await output.WriteAsync(inputBuf.AsMemory(0, read), ct);
            }
            else
                await output.WriteAsync(compBuf.AsMemory(0, compLen), ct);

            report?.Invoke(i + 1, blockCount);
        }

        indexTable[blockCount] = (uint)output.Position;
        output.Seek(indexOffset, SeekOrigin.Begin);

        var indexBytes = new byte[(blockCount + 1) * 4];

        for (int i = 0; i <= blockCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(indexBytes.AsSpan(i * 4), indexTable[i]);

        await output.WriteAsync(indexBytes, ct);
    }

    public static async Task CompressFromChdAsync(string chdPath, Stream output, byte version = 1, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var info = ChdInfoReader.ReadChdInfo(chdPath);

        using var wrapper = new LibChdrWrapper();
        var err = wrapper.Open(chdPath);

        if (err != ChdrError.CHDERR_NONE)
            throw new InvalidDataException($"CHD 열기 실패: {LibChdrWrapper.GetErrorString(err)}");


        if (info.SourceType == ChdSourceType.DVD)
        {
            long totalLength = (long)info.LogicalBytes;
            using var chdStream = new ChdReadStream(wrapper, totalLength);

            await CompressAsync(chdStream, output, version: version, progress: progress, ct: ct);
        }
        else if (info.SourceType == ChdSourceType.ISO)
        {
            long totalLength = (long)info.Tracks[0].Frames * 2048;
            using var chdStream = new ChdCdReadStream(wrapper, totalLength);

            await CompressAsync(chdStream, output, version: version, progress: progress, ct: ct);
        }
    }

    public async Task<bool> CompressToChdAsync(string isoPath, string chdPath, string compression = "zlib", IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) => await _chdman.CreateDvdAsync(isoPath, chdPath, compression, progress, ct);

    public async Task<bool> CompressCsoToChdAsync(string csoPath, string chdPath, IProgress<ProgressInfo>? progress = null, string compression = "zlib", CancellationToken ct = default)
    {
        var tmpIso = Utils.GetUniqueFilePath(Path.ChangeExtension(csoPath, ".iso"));

        try
        {
            await using (var csoStream = File.OpenRead(csoPath))
            await using (var isoStream = File.Create(tmpIso))
            {
                await DecompressAsync(csoStream, isoStream, progress, ct);
                await isoStream.FlushAsync(ct);
            }

            return await _chdman.CreateDvdAsync(tmpIso, chdPath, compression, progress, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
        finally
        {
            File.Delete(tmpIso);
        }
    }

    public async Task ExtractChdToIsoAsync(string chdPath, string isoPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var info = ChdInfoReader.ReadChdInfo(chdPath);

        if (info.SourceType == ChdSourceType.DVD)
            await _chdman.ExtractRawAsync(chdPath, isoPath, progress, ct);
        else
        {
            var cuePath = Path.ChangeExtension(isoPath, ".cue");
            var binPath = Path.ChangeExtension(isoPath, ".bin");

            try
            {
                await _chdman.ExtractCdAsync(chdPath, cuePath, progress, ct);

                if (File.Exists(binPath))
                    File.Move(binPath, isoPath, overwrite: true);

                if (File.Exists(cuePath))
                    File.Delete(cuePath);
            }
            catch
            {
                File.Delete(cuePath);
                File.Delete(binPath);

                throw;
            }
        }
    }
}