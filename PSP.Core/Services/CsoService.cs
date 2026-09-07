using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Models;
using CHD.Core.Services;
using Common;
using K4os.Compression.LZ4;
using LibDeflate;
using PSP.Core.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace PSP.Core.Services;

public class CsoService
{
    private const uint HeaderSize = 0x18;
    private const int IoBufferSize = 1 << 20;

    private readonly ChdmanService _chdman = new();

    public static Task DecompressAsync(Stream input, Stream output, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var bin = new BufferedStream(input, IoBufferSize);
            var bout = new BufferedStream(output, IoBufferSize);
            var magic = new byte[4];

            bin.ReadExactly(magic);

            bool isZso = magic.SequenceEqual(CsoHeader.MagicZSO);

            if (!isZso && !magic.SequenceEqual(CsoHeader.MagicCSO))
                throw new InvalidDataException("CSO/ZSO 매직 불일치");

            var headerBytes = new byte[HeaderSize - 4];

            bin.ReadExactly(headerBytes);

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
            uint decodeAlign = 1u << header.IndexShift;

            bin.ReadExactly(indexBytes);

            for (int i = 0; i <= blockCount; i++)
                indexTable[i] = BinaryPrimitives.ReadUInt32LittleEndian(indexBytes.AsSpan(i * 4));

            var compressed = new byte[header.BlockSize * 2];
            var blockBuf = new byte[header.BlockSize * 2];
            var reporter = progress is null ? null : new ProgressReporter("압축 해제 중...", string.Empty, blockCount, progress);
            var report = reporter?.CreateAction();
            using var deflateDecompressor = new DeflateDecompressor();
            long expectedPos = -1;

            for (int i = 0; i < blockCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                uint entry = indexTable[i];
                uint nextEntry = indexTable[i + 1];
                bool uncompressed = (entry & 0x80000000u) != 0;
                long offset = (long)(entry & 0x7FFFFFFFu) << header.IndexShift;
                long nextOffset = (long)(nextEntry & 0x7FFFFFFFu) << header.IndexShift;
                int blockLen = (int)(nextOffset - offset);

                if (offset != expectedPos)
                    bin.Seek(offset, SeekOrigin.Begin);

                bin.ReadExactly(compressed.AsSpan(0, blockLen));

                expectedPos = offset + blockLen;

                if (uncompressed)
                {
                    int rawSize = (int)Math.Min(header.BlockSize, header.UncompressedSize - (ulong)i * header.BlockSize);

                    bout.Write(compressed.AsSpan(0, rawSize));
                }
                else if (header.Version == 2 || isZso)
                {
                    int decoded = -1;
                    int maxTrim = (int)Math.Min(decodeAlign > 0 ? decodeAlign - 1 : 0, (uint)blockLen);

                    for (int trim = 0; trim <= maxTrim; trim++)
                    {
                        decoded = LZ4Codec.Decode(compressed, 0, blockLen - trim, blockBuf, 0, (int)header.BlockSize);

                        if (decoded >= 0)
                            break;
                    }

                    if (decoded < 0)
                        throw new InvalidDataException(
                            $"블록 {i}/{blockCount} LZ4 디코드 실패: blockLen={blockLen}, " +
                            $"offset={offset}, nextOffset={nextOffset}, IndexShift={header.IndexShift}, " +
                            $"UncompressedSize={header.UncompressedSize}, BlockSize={header.BlockSize}");

                    bout.Write(blockBuf.AsSpan(0, decoded));
                }
                else
                {
                    OperationStatus status = OperationStatus.InvalidData;
                    IMemoryOwner<byte>? owned = null;
                    int maxTrim = (int)Math.Min(decodeAlign > 0 ? decodeAlign - 1 : 0, (uint)blockLen);

                    for (int trim = 0; trim <= maxTrim; trim++)
                    {
                        status = deflateDecompressor.Decompress(compressed.AsSpan(0, blockLen - trim), (int)header.BlockSize, out owned);

                        if (status == OperationStatus.Done && owned is not null)
                            break;
                    }

                    if (status != OperationStatus.Done || owned is null)
                        throw new InvalidDataException($"블록 {i} 압축 해제 실패");

                    using (owned)
                        bout.Write(owned.Memory.Span);
                }

                report?.Invoke(i + 1, blockCount);
            }

            bout.Flush();
        }, ct);

    public static Task CompressAsync(Stream input, Stream output, byte[]? magic = null, byte version = 1, bool isLz4 = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            magic ??= CsoHeader.MagicCSO;
            isLz4 = isLz4 || version == 2;

            var bin = new BufferedStream(input, IoBufferSize);
            var bout = new BufferedStream(output, IoBufferSize);

            bout.Write(magic);

            const uint blockSize = 2048;
            long totalSize = input.Length;
            int blockCount = (int)Math.Ceiling((double)totalSize / blockSize);
            byte indexShift = ComputeIndexShift(totalSize + (blockCount + 1) * 4L, blockCount);
            uint align = 1u << indexShift;
            var headerBytes = new byte[HeaderSize - 4];

            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.AsSpan()[0..], HeaderSize);
            BinaryPrimitives.WriteUInt64LittleEndian(headerBytes.AsSpan()[4..], (ulong)totalSize);
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.AsSpan()[12..], blockSize);

            headerBytes[16] = version;
            headerBytes[17] = indexShift;

            bout.Write(headerBytes);

            long indexOffset = bout.Position;
            var indexTable = new uint[blockCount + 1];

            bout.Write(new byte[(blockCount + 1) * 4]);
            AlignPad(bout, align);

            var inputBuf = new byte[blockSize];
            var compBuf = new byte[blockSize * 2];
            var reporter = progress is null ? null : new ProgressReporter("압축 중...", string.Empty, blockCount, progress);
            var report = reporter?.CreateAction();
            using var compressor = isLz4 ? null : new DeflateCompressor(1);

            for (int i = 0; i < blockCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                int read = bin.ReadAtLeast(inputBuf, (int)Math.Min(blockSize, totalSize - (long)i * blockSize), throwOnEndOfStream: false);
                long blockOffset = bout.Position;
                long shiftedOffset = blockOffset >> indexShift;

                if (shiftedOffset > 0x7FFFFFFFL)
                    throw new InvalidOperationException($"IndexShift 계산 오류: 블록 {i} 오프셋({blockOffset})이 31비트 범위를 초과함");

                indexTable[i] = (uint)shiftedOffset;

                int compLen;
                bool useUncompressed;

                if (isLz4)
                {
                    compLen = LZ4Codec.Encode(inputBuf, 0, read, compBuf, 0, compBuf.Length);
                    useUncompressed = compLen <= 0 || compLen >= read;
                }
                else
                {
                    compLen = compressor!.Compress(inputBuf.AsSpan(0, read), compBuf);
                    useUncompressed = compLen <= 0 || compLen >= read;
                }

                if (useUncompressed)
                {
                    indexTable[i] |= 0x80000000u;
                    bout.Write(inputBuf.AsSpan(0, read));
                }
                else
                    bout.Write(compBuf.AsSpan(0, compLen));

                AlignPad(bout, align);

                report?.Invoke(i + 1, blockCount);
            }

            {
                long finalShifted = bout.Position >> indexShift;

                if (finalShifted > 0x7FFFFFFFL)
                    throw new InvalidOperationException($"IndexShift 계산 오류: 최종 오프셋({bout.Position})이 31비트 범위를 초과함");

                indexTable[blockCount] = (uint)finalShifted;
            }

            bout.Seek(indexOffset, SeekOrigin.Begin);

            var indexBytes = new byte[(blockCount + 1) * 4];

            for (int i = 0; i <= blockCount; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(indexBytes.AsSpan(i * 4), indexTable[i]);

            bout.Write(indexBytes);
            bout.Flush();
        }, ct);

    public static Task TranscodeAsync(Stream input, Stream output, byte[]? targetMagic = null, bool targetIsLz4 = false, byte targetVersion = 1, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var bin = new BufferedStream(input, IoBufferSize);
            var bout = new BufferedStream(output, IoBufferSize);
            var magic = new byte[4];

            bin.ReadExactly(magic);

            bool isZso = magic.SequenceEqual(CsoHeader.MagicZSO);

            if (!isZso && !magic.SequenceEqual(CsoHeader.MagicCSO))
                throw new InvalidDataException("CSO/ZSO 매직 불일치");

            var headerBytes = new byte[HeaderSize - 4];

            bin.ReadExactly(headerBytes);

            var header = new CsoHeader
            {
                HeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan()[0..]),
                UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.AsSpan()[4..]),
                BlockSize = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan()[12..]),
                Version = headerBytes[16],
                IndexShift = headerBytes[17],
            };

            int blockCount = (int)Math.Ceiling((double)header.UncompressedSize / header.BlockSize);
            var srcIndex = new uint[blockCount + 1];
            var srcIndexBytes = new byte[(blockCount + 1) * 4];

            bin.ReadExactly(srcIndexBytes);

            for (int i = 0; i <= blockCount; i++)
                srcIndex[i] = BinaryPrimitives.ReadUInt32LittleEndian(srcIndexBytes.AsSpan(i * 4));

            targetMagic ??= CsoHeader.MagicCSO;
            targetIsLz4 = targetIsLz4 || targetVersion == 2;

            byte dstIndexShift = ComputeIndexShift((long)header.UncompressedSize + (blockCount + 1) * 4L, blockCount);
            uint dstAlign = 1u << dstIndexShift;

            bout.Write(targetMagic);

            var dstHeaderBytes = new byte[HeaderSize - 4];

            BinaryPrimitives.WriteUInt32LittleEndian(dstHeaderBytes.AsSpan()[0..], HeaderSize);
            BinaryPrimitives.WriteUInt64LittleEndian(dstHeaderBytes.AsSpan()[4..], header.UncompressedSize);
            BinaryPrimitives.WriteUInt32LittleEndian(dstHeaderBytes.AsSpan()[12..], header.BlockSize);

            dstHeaderBytes[16] = targetVersion;
            dstHeaderBytes[17] = dstIndexShift;

            bout.Write(dstHeaderBytes);

            long dstIndexOffset = bout.Position;
            var dstIndex = new uint[blockCount + 1];

            bout.Write(new byte[(blockCount + 1) * 4]);

            AlignPad(bout, dstAlign);

            var compressed = new byte[header.BlockSize * 2];
            var rawBuf = new byte[header.BlockSize];
            var compBuf = new byte[header.BlockSize * 2];
            var reporter = progress is null ? null : new ProgressReporter("변환 중...", string.Empty, blockCount, progress);
            var report = reporter?.CreateAction();
            bool srcIsOldDeflate = header.Version != 2 && !isZso;
            bool dstIsOldDeflate = !targetIsLz4;
            using var deflateDecompressor = srcIsOldDeflate ? new DeflateDecompressor() : null;
            using var deflateCompressor = dstIsOldDeflate ? new DeflateCompressor(1) : null;

            long expectedPos = -1;

            for (int i = 0; i < blockCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                uint entry = srcIndex[i];
                uint nextEntry = srcIndex[i + 1];
                bool srcUncompressed = (entry & 0x80000000u) != 0;
                long offset = (long)(entry & 0x7FFFFFFFu) << header.IndexShift;
                long nextOffset = (long)(nextEntry & 0x7FFFFFFFu) << header.IndexShift;
                int blockLen = (int)(nextOffset - offset);

                if (offset != expectedPos)
                    bin.Seek(offset, SeekOrigin.Begin);

                bin.ReadExactly(compressed.AsSpan(0, blockLen));

                expectedPos = offset + blockLen;

                int rawLen;

                if (srcUncompressed)
                {
                    int rawSize = (int)Math.Min(header.BlockSize, header.UncompressedSize - (ulong)i * header.BlockSize);

                    compressed.AsSpan(0, rawSize).CopyTo(rawBuf);
                    rawLen = rawSize;
                }
                else if (header.Version == 2 || isZso)
                {
                    uint srcAlign = 1u << header.IndexShift;
                    int maxTrim = (int)Math.Min(srcAlign > 0 ? srcAlign - 1 : 0, (uint)blockLen);

                    rawLen = -1;

                    for (int trim = 0; trim <= maxTrim; trim++)
                    {
                        rawLen = LZ4Codec.Decode(compressed, 0, blockLen - trim, rawBuf, 0, (int)header.BlockSize);

                        if (rawLen >= 0)
                            break;
                    }

                    if (rawLen < 0)
                        throw new InvalidDataException(
                            $"블록 {i}/{blockCount} LZ4 디코드 실패: blockLen={blockLen}, " +
                            $"offset={offset}, nextOffset={nextOffset}, IndexShift={header.IndexShift}");
                }
                else
                {
                    uint srcAlign = 1u << header.IndexShift;
                    int maxTrim = (int)Math.Min(srcAlign > 0 ? srcAlign - 1 : 0, (uint)blockLen);

                    OperationStatus status = OperationStatus.InvalidData;
                    IMemoryOwner<byte>? owned = null;

                    for (int trim = 0; trim <= maxTrim; trim++)
                    {
                        status = deflateDecompressor!.Decompress(compressed.AsSpan(0, blockLen - trim), (int)header.BlockSize, out owned);

                        if (status == OperationStatus.Done && owned is not null)
                            break;
                    }

                    if (status != OperationStatus.Done || owned is null)
                        throw new InvalidDataException($"블록 {i} 압축 해제 실패");

                    using (owned)
                    {
                        rawLen = owned.Memory.Length;
                        owned.Memory.Span.CopyTo(rawBuf);
                    }
                }

                long dstBlockOffset = bout.Position;
                long dstShiftedOffset = dstBlockOffset >> dstIndexShift;

                if (dstShiftedOffset > 0x7FFFFFFFL)
                    throw new InvalidOperationException($"IndexShift 계산 오류: 블록 {i} 오프셋({dstBlockOffset})이 31비트 범위를 초과함");

                dstIndex[i] = (uint)dstShiftedOffset;

                int compLen;
                bool dstUncompressed;

                if (targetIsLz4)
                {
                    compLen = LZ4Codec.Encode(rawBuf, 0, rawLen, compBuf, 0, compBuf.Length);
                    dstUncompressed = compLen <= 0 || compLen >= rawLen;
                }
                else
                {
                    compLen = deflateCompressor!.Compress(rawBuf.AsSpan(0, rawLen), compBuf);
                    dstUncompressed = compLen <= 0 || compLen >= rawLen;
                }

                if (dstUncompressed)
                {
                    dstIndex[i] |= 0x80000000u;
                    bout.Write(rawBuf.AsSpan(0, rawLen));
                }
                else
                    bout.Write(compBuf.AsSpan(0, compLen));

                AlignPad(bout, dstAlign);

                report?.Invoke(i + 1, blockCount);
            }

            {
                long finalShifted = bout.Position >> dstIndexShift;

                if (finalShifted > 0x7FFFFFFFL)
                    throw new InvalidOperationException($"IndexShift 계산 오류: 최종 오프셋({bout.Position})이 31비트 범위를 초과함");

                dstIndex[blockCount] = (uint)finalShifted;
            }

            bout.Seek(dstIndexOffset, SeekOrigin.Begin);

            var dstIndexBytes = new byte[(blockCount + 1) * 4];

            for (int i = 0; i <= blockCount; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(dstIndexBytes.AsSpan(i * 4), dstIndex[i]);

            bout.Write(dstIndexBytes);
            bout.Flush();
        }, ct);

    private static byte ComputeIndexShift(long baseSize, int blockCount)
    {
        byte shift = 0;

        while (shift < 31)
        {
            long align = 1L << shift;
            long worstCaseSize = baseSize + (long)blockCount * (align - 1) + 4096;

            if (worstCaseSize < (0x80000000L << shift))
                break;

            shift++;
        }

        return shift;
    }

    private static void AlignPad(Stream stream, uint align)
    {
        if (align <= 1)
            return;

        long rem = stream.Position % align;

        if (rem != 0)
            stream.Write(new byte[align - rem]);
    }

    public static async Task CompressFromChdAsync(string chdPath, Stream output, byte[]? magic = null, byte version = 1, bool isLz4 = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
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

            await CompressAsync(chdStream, output, magic: magic, version: version, isLz4: isLz4, progress: progress, ct: ct);
        }
        else if (info.SourceType == ChdSourceType.ISO)
        {
            long totalLength = (long)info.Tracks[0].Frames * 2048;
            using var chdStream = new ChdCdReadStream(wrapper, totalLength);

            await CompressAsync(chdStream, output, magic: magic, version: version, isLz4: isLz4, progress: progress, ct: ct);
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