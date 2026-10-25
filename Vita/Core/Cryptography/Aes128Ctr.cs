using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Vita.Core.Cryptography;

public sealed class Aes128Ctr : IDisposable
{
    private const int MaxChunkBytes = 64 * 1024;

    private readonly Aes _aes;
    private readonly ulong _ivHigh;
    private readonly ulong _ivLow;

    public Aes128Ctr(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        if (key.Length != 16)
            throw new ArgumentException("key must be 16 bytes", nameof(key));

        if (iv.Length != 16)
            throw new ArgumentException("iv must be 16 bytes", nameof(iv));

        _aes = Aes.Create();
        _aes.Key = key.ToArray();

        _ivHigh = BinaryPrimitives.ReadUInt64BigEndian(iv);
        _ivLow = BinaryPrimitives.ReadUInt64BigEndian(iv[8..]);
    }

    public void XorAt(long blockOffset, Span<byte> buffer)
    {
        if (buffer.IsEmpty)
            return;

        ulong low = _ivLow + (ulong)blockOffset;
        ulong high = _ivHigh + (low < _ivLow ? 1UL : 0UL);
        int chunkBytes = Math.Min(MaxChunkBytes, (buffer.Length + 15) & ~15);
        byte[] scratch = ArrayPool<byte>.Shared.Rent(chunkBytes);

        try
        {
            int processed = 0;

            while (processed < buffer.Length)
            {
                int length = Math.Min(chunkBytes, buffer.Length - processed);
                int blocks = (length + 15) / 16;
                var counters = scratch.AsSpan(0, blocks * 16);

                for (int i = 0; i < blocks; i++)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(counters[(i * 16)..], high);
                    BinaryPrimitives.WriteUInt64BigEndian(counters[(i * 16 + 8)..], low);

                    low++;

                    if (low == 0)
                        high++;
                }

                _aes.EncryptEcb(counters, counters, PaddingMode.None);
                Xor(buffer.Slice(processed, length), counters);

                processed += length;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private static void Xor(Span<byte> data, ReadOnlySpan<byte> keystream)
    {
        int done = 0;

        if (Vector.IsHardwareAccelerated)
        {
            var dataVectors = MemoryMarshal.Cast<byte, Vector<byte>>(data);
            var keyVectors = MemoryMarshal.Cast<byte, Vector<byte>>(keystream[..data.Length]);

            for (int i = 0; i < dataVectors.Length; i++)
                dataVectors[i] ^= keyVectors[i];

            done = dataVectors.Length * Vector<byte>.Count;
        }

        for (int i = done; i < data.Length; i++)
            data[i] ^= keystream[i];
    }

    public static byte[] EcbEncryptSingleBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> block)
    {
        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key.ToArray();

        using var encryptor = aes.CreateEncryptor();
        var input = block.ToArray();
        var output = new byte[16];

        encryptor.TransformBlock(input, 0, 16, output, 0);

        return output;
    }

    public void Dispose() => _aes.Dispose();
}