using Common;
using LibHac.Common.FixedArrays;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;
using ZstdSharp.Unsafe;
using Blake3;

namespace LibHac.NSZ;

public class NcaToNczConverter(KeySet keySet)
{
    private const long HeaderSize = 0x4000;
    private const int ChunkSize = 0x2000000;
    private const int DefaultBlockSizeExponent = 20;

    private const string StreamingSectionMagic = "NCZSECTN";
    private const string BlockSectionMagic = "NCZBLOCK";

    private byte[] _originalHash;

    private static readonly ArrayPool<byte> _chunkPool = ArrayPool<byte>.Shared;

    public async Task ConvertAsync(MemoryStream headerStream, IStorage originalStorage, Stream outputStream, bool useBlockMode, int compressionLevel, Action<long> onRead = null, CancellationToken ct = default)
    {
        var nca = new Nca(keySet, originalStorage);

        bool hasSparse = false;
        for (int i = 0; i < 4; i++)
        {
            if (!nca.SectionExists(i)) continue;
            if (nca.GetFsHeader(i).ExistsSparseLayer())
            {
                hasSparse = true;
                break;
            }
        }

        if (hasSparse)
        {
            await global::Common.Utils.CopyStreamAsync(originalStorage.AsStream(), outputStream, onRead, ct);
            return;
        }

        if (useBlockMode)
            await ConvertBlockAsync(headerStream, originalStorage, outputStream, compressionLevel, onRead, ct);
        else
            await ConvertBlocklessAsync(headerStream, originalStorage, outputStream, compressionLevel, onRead, ct);
    }

    private async Task ConvertBlocklessAsync(MemoryStream headerStream, IStorage originalStorage, Stream outputStream, int compressionLevel, Action<long> onRead = null, CancellationToken ct = default)
    {
        _originalHash = null;

        var nca = new Nca(keySet, originalStorage);

        byte[] rawHeader;
        if (headerStream != null)
        {
            rawHeader = new byte[HeaderSize];
            originalStorage.Read(0, rawHeader).ThrowIfFailure();
            Array.Copy(headerStream.ToArray(), 0, rawHeader, 0, headerStream.Length);
        }
        else
        {
            rawHeader = new byte[HeaderSize];
            originalStorage.Read(0, rawHeader).ThrowIfFailure();
        }
        await outputStream.WriteAsync(rawHeader, ct);

        var sections = CollectSections(nca);
        WriteSectionTable(outputStream, sections, StreamingSectionMagic);

        using IStorage decryptedStorage = nca.OpenDecryptedNca();
        decryptedStorage.GetSize(out long ncaSize).ThrowIfFailure();

        using var hasher = Hasher.New();

        int threadCount = Math.Min(Environment.ProcessorCount, 8);

        var compressorStack = new ConcurrentStack<Compressor>();
        for (int i = 0; i < threadCount; i++)
        {
            var c = new Compressor(compressionLevel);
            c.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, 25);
            c.SetParameter(ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching, 1);
            compressorStack.Push(c);
        }

        var writeQueue = new Queue<Task<(byte[] data, int plainSize, byte[] rentedBuf)>>();
        long readPos = HeaderSize;

        using var semaphore = new SemaphoreSlim(threadCount);

        try
        {
            while (readPos < ncaSize)
            {
                ct.ThrowIfCancellationRequested();
                await semaphore.WaitAsync(ct);

                int toRead = (int)Math.Min(ChunkSize, ncaSize - readPos);
                byte[] rentedBuf = _chunkPool.Rent(toRead);

                var readRes = decryptedStorage.Read(readPos, rentedBuf.AsSpan(0, toRead));
                if (readRes != Result.Success) throw new Exception("NCA Read Failed");

                hasher.Update(rentedBuf.AsSpan(0, toRead));

                onRead?.Invoke(toRead / 2);

                int currentReadSize = toRead;
                var task = Task.Run(() =>
                {
                    try
                    {
                        if (compressorStack.TryPop(out var compressor))
                        {
                            try
                            {
                                byte[] compressed = compressor.Wrap(new ReadOnlySpan<byte>(rentedBuf, 0, currentReadSize)).ToArray();
                                return (data: compressed, plainSize: currentReadSize, rentedBuf);
                            }
                            finally
                            {
                                compressorStack.Push(compressor);
                            }
                        }
                        throw new Exception("Compressor 획득 실패");
                    }
                    catch
                    {
                        _chunkPool.Return(rentedBuf);
                        semaphore.Release();
                        throw;
                    }
                }, ct);

                writeQueue.Enqueue(task);
                readPos += toRead;

                if (writeQueue.Count >= threadCount)
                    await FlushNextAsync();
            }

            while (writeQueue.Count > 0)
                await FlushNextAsync();
        }
        finally
        {
            foreach (var c in compressorStack) c.Dispose();
            _originalHash = hasher.Finalize().AsSpan().ToArray();
        }

        async Task FlushNextAsync()
        {
            var task = writeQueue.Dequeue();
            var (data, plainSize, rentedBuf) = await task;

            try
            {
                await outputStream.WriteAsync(data, ct);
                onRead?.Invoke(plainSize - (plainSize / 2));
            }
            finally
            {
                _chunkPool.Return(rentedBuf);
                semaphore.Release();
            }
        }
    }

    private async Task ConvertBlockAsync(MemoryStream headerStream, IStorage originalStorage, Stream outputStream, int compressionLevel, Action<long> onRead = null, CancellationToken ct = default)
    {
        _originalHash = null;

        int blockSizeExponent = DefaultBlockSizeExponent;
        int blockSize = 1 << blockSizeExponent;

        var nca = new Nca(keySet, originalStorage);

        byte[] rawHeader;
        if (headerStream != null)
        {
            rawHeader = new byte[HeaderSize];
            originalStorage.Read(0, rawHeader).ThrowIfFailure();
            Array.Copy(headerStream.ToArray(), 0, rawHeader, 0, headerStream.Length);
        }
        else
        {
            rawHeader = new byte[HeaderSize];
            originalStorage.Read(0, rawHeader).ThrowIfFailure();
        }
        await outputStream.WriteAsync(rawHeader, ct);

        var sections = CollectSections(nca);

        if (sections.Count == 0)
            throw new InvalidDataException("No compressible sections found.");

        WriteSectionTable(outputStream, sections, StreamingSectionMagic);

        using IStorage decryptedStorage = nca.OpenDecryptedNca();
        decryptedStorage.GetSize(out long ncaSize).ThrowIfFailure();

        long dataLength = ncaSize - HeaderSize;
        int blockCount = (int)((dataLength + blockSize - 1) / blockSize);

        outputStream.Write(Encoding.ASCII.GetBytes(BlockSectionMagic));

        long blockHeaderPos = outputStream.Position;
        int blockHeaderSize = 1 + 1 + 1 + 1 + 4 + 8 + blockCount * 4;

        outputStream.Write(new byte[blockHeaderSize]);

        using var hasher = Hasher.New();
        var compressedSizes = new int[blockCount];

        int maxConcurrentBlocks = Math.Min(Environment.ProcessorCount, 8);
        var semaphore = new SemaphoreSlim(maxConcurrentBlocks);
        var pool = ArrayPool<byte>.Shared;
        var writeQueue = new Queue<Task<(byte[] data, int plainSize, byte[] rawBuffer)>>();
        int writeIndex = 0;

        try
        {
            for (int b = 0; b < blockCount; b++)
            {
                ct.ThrowIfCancellationRequested();
                await semaphore.WaitAsync(ct);

                long readPos = HeaderSize + (long)b * blockSize;
                int toRead = (int)Math.Min(blockSize, ncaSize - readPos);
                byte[] buffer = pool.Rent(toRead);

                decryptedStorage.Read(readPos, buffer.AsSpan(0, toRead)).ThrowIfFailure();

                hasher.Update(buffer.AsSpan(0, toRead));

                onRead?.Invoke(toRead / 2);

                var compressTask = Task.Run(() =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();

                        using var compressor = new Compressor(compressionLevel);
                        byte[] compressedBuffer = GC.AllocateUninitializedArray<byte>(Compressor.GetCompressBound(toRead));
                        int compressedSize = compressor.Wrap(new ReadOnlySpan<byte>(buffer, 0, toRead), new Span<byte>(compressedBuffer));

                        if (compressedSize < toRead)
                        {
                            byte[] finalData = GC.AllocateUninitializedArray<byte>(compressedSize);
                            Unsafe.CopyBlock(ref finalData[0], ref compressedBuffer[0], (uint)compressedSize);
                            return (data: finalData, plainSize: toRead, rawBuffer: buffer);
                        }
                        else
                        {
                            byte[] rawCopy = GC.AllocateUninitializedArray<byte>(toRead);
                            Unsafe.CopyBlock(ref rawCopy[0], ref buffer[0], (uint)toRead);
                            return (data: rawCopy, plainSize: toRead, rawBuffer: buffer);
                        }
                    }
                    catch
                    {
                        pool.Return(buffer);
                        semaphore.Release();
                        throw;
                    }
                }, ct);

                writeQueue.Enqueue(compressTask);

                if (writeQueue.Count >= maxConcurrentBlocks)
                    await FlushNext();
            }

            while (writeQueue.Count > 0)
                await FlushNext();
        }
        finally
        {
            _originalHash = hasher.Finalize().AsSpan().ToArray();
        }

        outputStream.Position = blockHeaderPos;
        WriteBlockHeader(outputStream, blockSizeExponent, blockCount, compressedSizes, dataLength);
        outputStream.Position = outputStream.Length;

        async Task FlushNext()
        {
            var (data, plainSize, rawBuffer) = await writeQueue.Dequeue();

            try
            {
                compressedSizes[writeIndex++] = data.Length;
                await outputStream.WriteAsync(data, ct);

                onRead?.Invoke(plainSize - (plainSize / 2));
            }
            finally
            {
                pool.Return(rawBuffer);
                semaphore.Release();
            }
        }
    }

    public async Task ValidateAsync(Stream nczStream, string titleId, long totalVerifySize, string label, IProgress<ProgressInfo> progress, CancellationToken ct)
    {
        if (_originalHash == null)
            return;

        nczStream.Position = 0;
        var ncz = new Ncz(keySet, nczStream, NczReadMode.Original);
        using IStorage decrypted = ncz.OpenDecryptedNca();
        decrypted.GetSize(out long decSize).ThrowIfFailure();

        using var hasher = Hasher.New();
        var reportSw = Stopwatch.StartNew();
        var startTime = Stopwatch.GetTimestamp();
        double freq = Stopwatch.Frequency;
        long currentVerifyPos = 0;
        long startVerifyPos = currentVerifyPos;

        var channel = System.Threading.Channels.Channel.CreateBounded<byte[]>(10);

        var readTask = Task.Run(async () =>
        {
            try
            {
                long readPos = HeaderSize;
                while (readPos < decSize)
                {
                    ct.ThrowIfCancellationRequested();
                    int toRead = (int)Math.Min(ChunkSize, decSize - readPos);
                    byte[] buf = new byte[toRead];
                    decrypted.Read(readPos, buf.AsSpan()).ThrowIfFailure();
                    await channel.Writer.WriteAsync(buf, ct);
                    readPos += toRead;
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        await foreach (var data in channel.Reader.ReadAllAsync(ct))
        {
            hasher.Update(data.AsSpan());

            currentVerifyPos += data.Length;

            if (reportSw.ElapsedMilliseconds >= 100)
            {
                long now = Stopwatch.GetTimestamp();
                double elapsedSec = Math.Max(0.001, (double)(now - startTime) / freq);
                long processedInThisFile = currentVerifyPos - startVerifyPos;
                double bytesPerSec = processedInThisFile / elapsedSec;
                double remainingBytes = Math.Max(0, totalVerifySize - currentVerifyPos);
                double etaSec = bytesPerSec > 1024 ? remainingBytes / bytesPerSec : 0;
                var elapsed = System.TimeSpan.FromSeconds(elapsedSec);
                double totalEtaRaw = elapsedSec + etaSec;
                if (double.IsInfinity(totalEtaRaw) || totalEtaRaw > 8640000) totalEtaRaw = elapsedSec;
                var totalEta = System.TimeSpan.FromSeconds(totalEtaRaw);

                int pct = totalVerifySize > 0
                    ? (int)Math.Min(100, currentVerifyPos * 100 / totalVerifySize)
                    : 0;

                var r = global::Common.Utils.CalculateProgress(currentVerifyPos, totalVerifySize, label);
                progress?.Report(new ProgressInfo(
                    pct,
                    $"Decompressing and hashing... {r.label}",
                    titleId,
                    $"{(bytesPerSec / (1024.0 * 1024.0)):F1} MiB/s",
                    $"{elapsed:mm\\:ss} / {totalEta:mm\\:ss}"
                ));

                reportSw.Restart();
            }
        }

        await readTask;

        if (!hasher.Finalize().AsSpan().SequenceEqual(_originalHash))
            throw new InvalidDataException($"{label} verification failed: hash mismatch");
    }

    private static void WriteBlockHeader(Stream output, int blockSizeExponent, int blockCount, int[] compressedSizes, long decompressedSize)
    {
        output.WriteByte(2);
        output.WriteByte(1);
        output.WriteByte(0);
        output.WriteByte((byte)blockSizeExponent);
        output.Write(BitConverter.GetBytes(blockCount));
        output.Write(BitConverter.GetBytes(decompressedSize));

        foreach (int size in compressedSizes)
            output.Write(BitConverter.GetBytes(size));
    }

    private static void WriteSectionTable(Stream output, List<NczSectionRaw> sections, string magic)
    {
        output.Write(Encoding.ASCII.GetBytes(magic));
        output.Write(BitConverter.GetBytes((long)sections.Count));

        foreach (var s in sections)
        {
            output.Write(BitConverter.GetBytes(s.Offset));
            output.Write(BitConverter.GetBytes(s.Size));
            output.Write(BitConverter.GetBytes(s.CryptoType));
            output.Write(new byte[8]);
            output.Write(s.CryptoKey);
            output.Write(s.CryptoCounter);
        }
    }

    private static List<NczSectionRaw> CollectSections(Nca nca)
    {
        var result = new List<NczSectionRaw>();

        for (int i = 0; i < 4; i++)
        {
            if (!nca.SectionExists(i)) continue;

            var fsHeader = nca.GetFsHeader(i);
            var patchInfo = fsHeader.GetPatchInfo();

            long sectionOffset = nca.Header.GetSectionStartOffset(i);
            long sectionSize = nca.Header.GetSectionSize(i);

            if (fsHeader.EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                var extStorage = nca.OpenAesCtrCounterExtendedStorage(i);
                if (extStorage == null) continue;

                extStorage.ReadAllEntries(out var entries).ThrowIfFailure();
                extStorage.GetSize(out long extSize).ThrowIfFailure();
                byte[] key = extStorage.GetKey().ToArray();
                uint secureValue = extStorage.GetSecureValue();

                for (int e = 0; e < entries.Length; e++)
                {
                    var entry = entries[e];
                    long entryOffset = sectionOffset + entry.GetOffset();
                    long entryEnd = e + 1 < entries.Length
                        ? sectionOffset + entries[e + 1].GetOffset()
                        : sectionOffset + extSize;

                    var section = new NczSectionRaw
                    {
                        Offset = entryOffset,
                        Size = entryEnd - entryOffset,
                        CryptoType = entry.EncryptionValue == AesCtrCounterExtendedStorage.Entry.Encryption.Encrypted
                            ? (long)NcaEncryptionType.AesCtr
                            : (long)NcaEncryptionType.None,
                        CryptoKey = new byte[16],
                        CryptoCounter = new byte[16]
                    };

                    if (entry.EncryptionValue == AesCtrCounterExtendedStorage.Entry.Encryption.Encrypted)
                    {
                        NcaAesCtrUpperIv upperIv = new() { Generation = (uint)entry.Generation, SecureValue = secureValue };
                        Unsafe.SkipInit(out Array16<byte> counter);
                        AesCtrStorage.MakeIv(counter, upperIv.Value, 0);

                        key.CopyTo(section.CryptoKey.AsSpan());
                        counter[..].CopyTo(section.CryptoCounter.AsSpan());
                    }
                    result.Add(section);
                }
                long lastSectionEnd = sectionOffset + extSize;
                long bktrEnd = sectionOffset + sectionSize;
                if (bktrEnd > lastSectionEnd)
                {
                    byte[] bktrCounter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, 0);
                    result.Add(new NczSectionRaw
                    {
                        Offset = lastSectionEnd,
                        Size = bktrEnd - lastSectionEnd,
                        CryptoType = (long)NcaEncryptionType.AesCtr,
                        CryptoKey = [.. key],
                        CryptoCounter = bktrCounter
                    });
                }
                continue;
            }

            var s = new NczSectionRaw
            {
                Offset = sectionOffset,
                Size = sectionSize,
                CryptoType = (long)fsHeader.EncryptionType,
                CryptoKey = new byte[16],
                CryptoCounter = new byte[16]
            };

            if (fsHeader.EncryptionType == NcaEncryptionType.AesCtr)
            {
                nca.GetContentKey(NcaKeyType.AesCtr).CopyTo(s.CryptoKey.AsSpan());
                ulong upperIv = fsHeader.Counter;
                byte[] counterBytes = BitConverter.GetBytes(upperIv);
                if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);
                counterBytes.CopyTo(s.CryptoCounter, 0);
            }

            result.Add(s);
        }

        result.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        if (result.Count > 0 && result[0].Offset < HeaderSize)
        {
            long overlap = HeaderSize - result[0].Offset;
            var first = result[0];
            first.Offset = HeaderSize;
            first.Size -= overlap;

            if (first.Size <= 0)
                result.RemoveAt(0);
            else
                result[0] = first;
        }

        return result;
    }
}