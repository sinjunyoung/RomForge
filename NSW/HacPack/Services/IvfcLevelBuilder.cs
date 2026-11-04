using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using NSW.HacPack.Models;

namespace NSW.HacPack.Services;

public sealed class IvfcLevelBuilder
{
    private const int BlockSize = IvfcConstants.HashBlockSize;
    private const int HashSize = 32;
    private const int ParallelBlocks = 64;

    private readonly MemoryStream _hashes = new();
    private readonly byte[] _tail = new byte[BlockSize];
    private int _tailLength;

    public void Add(byte[] data, int offset, int count)
    {
        if (_tailLength > 0 && count > 0)
        {
            int take = Math.Min(BlockSize - _tailLength, count);

            Buffer.BlockCopy(data, offset, _tail, _tailLength, take);

            _tailLength += take;
            offset += take;
            count -= take;

            if (_tailLength == BlockSize)
            {
                AppendTail();

                _tailLength = 0;
            }
        }

        int fullBlocks = count / BlockSize;

        if (fullBlocks > 0)
        {
            HashBlocks(data, offset, fullBlocks);

            offset += fullBlocks * BlockSize;
            count -= fullBlocks * BlockSize;
        }

        if (count > 0)
        {
            Buffer.BlockCopy(data, offset, _tail, 0, count);

            _tailLength = count;
        }
    }

    public void Complete(Stream output, out ulong generatedSize)
    {
        if (_tailLength > 0)
        {
            AppendTail();

            _tailLength = 0;
        }

        _hashes.WriteTo(output);

        int paddingSize = (int)(BlockSize - output.Position % BlockSize);

        if (paddingSize != BlockSize)
            output.Write(new byte[paddingSize]);

        generatedSize = (ulong)output.Position;
    }

    private void AppendTail()
    {
        Span<byte> hash = stackalloc byte[HashSize];

        SHA256.HashData(_tail.AsSpan(0, _tailLength), hash);
        _hashes.Write(hash);
    }

    private void HashBlocks(byte[] data, int offset, int blocks)
    {
        byte[] hashes = ArrayPool<byte>.Shared.Rent(blocks * HashSize);

        try
        {
            if (blocks >= ParallelBlocks && Environment.ProcessorCount > 1)
            {
                int parts = Math.Min(Environment.ProcessorCount, blocks / 16);

                Parallel.For(0, parts, part =>
                {
                    int from = (int)((long)blocks * part / parts);
                    int to = (int)((long)blocks * (part + 1) / parts);

                    HashRange(data, offset, hashes, from, to);
                });
            }
            else
                HashRange(data, offset, hashes, 0, blocks);

            _hashes.Write(hashes, 0, blocks * HashSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(hashes);
        }
    }

    private static void HashRange(byte[] data, int offset, byte[] hashes, int from, int to)
    {
        for (int i = from; i < to; i++)
            SHA256.HashData(data.AsSpan(offset + i * BlockSize, BlockSize), hashes.AsSpan(i * HashSize, HashSize));
    }
}