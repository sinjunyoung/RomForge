using System.Buffers;
using Common.Localization;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace NSW.HacPack.Cryptography.Core;

public sealed class AesCtrCipher : IDisposable
{
    private const int BlockSize = 16;
    private const int StripeBytes = 1 << 20;
    private const int ParallelThreshold = 1 << 22;
    private const int ParallelPartSize = 1 << 21;

    private readonly byte[] _key;
    private readonly ulong _nonce;
    private readonly Aes _aes;

    public AesCtrCipher(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce)
    {
        _key = key.ToArray();
        _nonce = MemoryMarshal.Read<ulong>(nonce);
        _aes = CreateAes(_key);
    }

    public static AesCtrCipher ForSection(ReadOnlySpan<byte> key, ReadOnlySpan<byte> sectionCtr)
    {
        Span<byte> nonce = stackalloc byte[8];

        for (int i = 0; i < 8; i++)
            nonce[i] = sectionCtr[7 - i];

        return new AesCtrCipher(key, nonce);
    }

    public void Transform(ReadOnlySpan<byte> source, Span<byte> destination, ulong offset)
    {
        if (destination.Length < source.Length)
            throw new ArgumentException(Loc.S.ErrDestBufferTooSmall, nameof(destination));

        TransformSerial(_aes, source, destination, offset);
    }

    public void Transform(byte[] source, int sourceOffset, byte[] destination, int destinationOffset, int count, ulong offset)
    {
        if (count < ParallelThreshold || Environment.ProcessorCount == 1)
        {
            TransformSerial(_aes, source.AsSpan(sourceOffset, count), destination.AsSpan(destinationOffset, count), offset);
            return;
        }

        int parts = (count + ParallelPartSize - 1) / ParallelPartSize;
        byte[] key = _key;

        Parallel.For(0, parts, () => CreateAes(key), (part, _, aes) =>
        {
            int start = part * ParallelPartSize;
            int length = Math.Min(ParallelPartSize, count - start);

            TransformSerial(aes, source.AsSpan(sourceOffset + start, length), destination.AsSpan(destinationOffset + start, length), offset + (ulong)start);

            return aes;
        }, aes => aes.Dispose());
    }

    public void Dispose() => _aes.Dispose();

    private static Aes CreateAes(byte[] key)
    {
        var aes = Aes.Create();

        aes.Key = key;

        return aes;
    }

    private void TransformSerial(Aes aes, ReadOnlySpan<byte> source, Span<byte> destination, ulong offset)
    {
        byte[] counters = ArrayPool<byte>.Shared.Rent(StripeBytes + BlockSize);
        byte[] keystream = ArrayPool<byte>.Shared.Rent(StripeBytes + BlockSize);

        try
        {
            int done = 0;

            while (done < source.Length)
            {
                ulong position = offset + (ulong)done;
                ulong block = position >> 4;
                int skip = (int)(position & (BlockSize - 1));
                int length = Math.Min(source.Length - done, StripeBytes - skip);
                int blocks = (skip + length + BlockSize - 1) / BlockSize;

                FillCounters(counters, blocks, block);
                aes.EncryptEcb(counters.AsSpan(0, blocks * BlockSize), keystream.AsSpan(0, blocks * BlockSize), PaddingMode.None);
                Xor(source.Slice(done, length), keystream.AsSpan(skip, length), destination.Slice(done, length));

                done += length;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(counters);
            ArrayPool<byte>.Shared.Return(keystream);
        }
    }

    private void FillCounters(byte[] counters, int blocks, ulong firstBlock)
    {
        Span<ulong> words = MemoryMarshal.Cast<byte, ulong>(counters.AsSpan(0, blocks * BlockSize));
        ulong nonce = _nonce;

        for (int i = 0; i < blocks; i++)
        {
            words[2 * i] = nonce;
            words[2 * i + 1] = BinaryPrimitives.ReverseEndianness(firstBlock + (ulong)i);
        }
    }

    private static void Xor(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, Span<byte> destination)
    {
        int i = 0;

        if (Vector.IsHardwareAccelerated)
        {
            int width = Vector<byte>.Count;

            for (; i <= left.Length - width; i += width)
                (new Vector<byte>(left[i..]) ^ new Vector<byte>(right[i..])).CopyTo(destination[i..]);
        }

        for (; i < left.Length; i++)
            destination[i] = (byte)(left[i] ^ right[i]);
    }
}