using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class Aes128CtrTransform
{
    private const int BlockSize = 128;
    private const int BlockSizeBytes = BlockSize / 8;

    public readonly byte[] Counter = new byte[BlockSizeBytes];

    private readonly ICryptoTransform _encryptor;

    // nsz 복호화 버그 수정을 위해 추가
    private readonly byte[] _initialCounter = new byte[16];
    private long _currentPos = -1;

    public Aes128CtrTransform(byte[] key, byte[] counter)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (counter == null) throw new ArgumentNullException(nameof(counter));
        if (key.Length != BlockSizeBytes)
            throw new ArgumentException($"{nameof(key)} must be {BlockSizeBytes} bytes long");
        if (counter.Length != BlockSizeBytes)
            throw new ArgumentException($"{nameof(counter)} must be {BlockSizeBytes} bytes long");

        Aes aes = Aes.Create();
        if (aes == null) throw new CryptographicException("Unable to create AES object");
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        _encryptor = aes.CreateEncryptor(key, new byte[BlockSizeBytes]);

        Array.Copy(counter, _initialCounter, BlockSizeBytes);
        Array.Copy(counter, Counter, BlockSizeBytes);
    }

    public int TransformBlock(Span<byte> data)
    {
        int blockCount = BitUtil.DivideUp(data.Length, BlockSizeBytes);
        int length = blockCount * BlockSizeBytes;

        byte[] counterXor = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Counter.CopyTo(counterXor, 0);
            FillDecryptedCounter(counterXor.AsSpan(0, length));

            _encryptor.TransformBlock(counterXor, 0, length, counterXor, 0);
            Utilities.XorArrays(data, counterXor);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(counterXor);
        }

        return data.Length;
    }

    /// <summary>
    /// nsz 복호화 버그 수정을 위해 추가
    /// </summary>
    /// <param name="ncaOffset"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public int TransformBlock(long ncaOffset, Span<byte> data)
    {
        if (data.Length == 0) return 0;

        if (_currentPos != ncaOffset)
        {
            ulong blockOffset = (ulong)(ncaOffset / BlockSizeBytes);
            Array.Copy(_initialCounter, Counter, 16);
            Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(Counter.AsSpan());
            ulong lo = BinaryPrimitives.ReverseEndianness(bufL[1]);
            lo += blockOffset;
            bufL[1] = BinaryPrimitives.ReverseEndianness(lo);

            _currentPos = ncaOffset;
        }

        int innerOffset = (int)(_currentPos % BlockSizeBytes);
        int totalLength = BitUtil.DivideUp(data.Length + innerOffset, BlockSizeBytes) * BlockSizeBytes;

        byte[] counterXor = ArrayPool<byte>.Shared.Rent(totalLength);
        try
        {
            Counter.CopyTo(counterXor, 0);
            FillDecryptedCounter(counterXor.AsSpan(0, totalLength));
            _encryptor.TransformBlock(counterXor, 0, totalLength, counterXor, 0);

            var validKeyStream = counterXor.AsSpan(innerOffset, data.Length);
            Utilities.XorArrays(data, validKeyStream);

            _currentPos += data.Length;

            UpdateCounterToOffset(_currentPos);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(counterXor);
        }

        return data.Length;
    }

    private void UpdateCounterToOffset(long offset)
    {
        ulong blockOffset = (ulong)(offset / BlockSizeBytes);
        Array.Copy(_initialCounter, Counter, 16);

        Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(Counter.AsSpan());
        ulong lo = BinaryPrimitives.ReverseEndianness(bufL[1]);
        lo += blockOffset;
        bufL[1] = BinaryPrimitives.ReverseEndianness(lo);
    }

    public static void FillDecryptedCounter(Span<byte> buffer)
    {
        Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(buffer);

        ulong hi = bufL[0];
        ulong lo = BinaryPrimitives.ReverseEndianness(bufL[1]);

        for (int i = 2; i < bufL.Length; i += 2)
        {
            lo++;
            bufL[i] = hi;
            bufL[i + 1] = BinaryPrimitives.ReverseEndianness(lo);
        }
    }
}