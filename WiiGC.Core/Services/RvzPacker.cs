using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal sealed class RvzPacker
{
    private const int SeedBytes = LaggedFibonacciGenerator.SeedBytes;

    private readonly LaggedFibonacciGenerator _generator = new();
    private readonly byte[] _scratchSeed = new byte[SeedBytes];
    private int[] _ends = new int[16];
    private int[] _starts = new int[16];
    private byte[] _seeds = new byte[16 * SeedBytes];
    private int _junkCount;
    private byte[] _output = [];
    private int _outputLength;

    public ReadOnlySpan<byte> Output => _output.AsSpan(0, _outputLength);

    public byte[] OutputBuffer => _output;

    public uint PackedSize { get; private set; }

    public bool Pack(ReadOnlySpan<byte> data, long dataOffset, bool compression)
    {
        _junkCount = 0;
        _outputLength = 0;
        PackedSize = 0;

        FindJunk(data, dataOffset, compression);

        return Emit(data);
    }

    private void FindJunk(ReadOnlySpan<byte> data, long dataOffset, bool compression)
    {
        int total = data.Length;
        int position = 0;
        long offset = dataOffset;

        while (position < total)
        {
            var rest = data[position..];
            int firstNonZero = rest.IndexOfAnyExcept((byte)0);
            int zeroes = firstNonZero < 0 ? rest.Length : firstNonZero;

            if (!compression && zeroes > SeedBytes)
            {
                Array.Clear(_scratchSeed);
                AddJunk(position + zeroes, position, _scratchSeed);
            }

            position += zeroes;
            offset += zeroes;

            long alignedNext = (offset + 1 + WiiLayout.BlockTotalSize - 1) / WiiLayout.BlockTotalSize * WiiLayout.BlockTotalSize;
            int bytesToRead = (int)Math.Min(alignedNext - offset, total - position);
            int offsetInBlock = (int)(offset % WiiLayout.BlockTotalSize);
            int reconstructed = _generator.GetSeed(data.Slice(position, bytesToRead), offsetInBlock, _scratchSeed);

            if (reconstructed > 0)
                AddJunk(position + reconstructed, position, _scratchSeed);

            position += bytesToRead;
            offset += bytesToRead;
        }
    }

    private void AddJunk(int end, int start, ReadOnlySpan<byte> seed)
    {
        if (_junkCount > 0 && _ends[_junkCount - 1] >= end)
            return;

        if (_junkCount == _ends.Length)
        {
            int size = _junkCount * 2;

            Array.Resize(ref _ends, size);
            Array.Resize(ref _starts, size);
            Array.Resize(ref _seeds, size * SeedBytes);
        }

        _ends[_junkCount] = end;
        _starts[_junkCount] = start;

        seed.CopyTo(_seeds.AsSpan(_junkCount * SeedBytes, SeedBytes));

        _junkCount++;
    }

    private int UpperBound(int key)
    {
        int low = 0;
        int high = _junkCount;

        while (low < high)
        {
            int mid = (low + high) >>> 1;

            if (_ends[mid] > key)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    private bool Emit(ReadOnlySpan<byte> data)
    {
        int end = data.Length;
        int current = 0;
        bool first = true;

        while (current < end)
        {
            int nextJunkStart = end;
            int nextJunkEnd = end;
            int seedIndex = -1;

            if (end - current > SeedBytes)
            {
                int index = UpperBound(current + SeedBytes);

                if (index < _junkCount && _starts[index] + SeedBytes < end)
                {
                    nextJunkStart = Math.Max(current, _starts[index]);
                    nextJunkEnd = Math.Min(end, _ends[index]);
                    seedIndex = index;
                }
            }

            if (first)
            {
                if (nextJunkStart == end)
                    return false;

                first = false;
            }

            int nonJunk = nextJunkStart - current;

            if (nonJunk > 0)
            {
                Reserve(sizeof(uint) + nonJunk);
                BinaryPrimitives.WriteUInt32BigEndian(_output.AsSpan(_outputLength), (uint)nonJunk);
                data.Slice(current, nonJunk).CopyTo(_output.AsSpan(_outputLength + sizeof(uint)));

                _outputLength += sizeof(uint) + nonJunk;
                current += nonJunk;

                PackedSize += (uint)(sizeof(uint) + nonJunk);
            }

            int junk = nextJunkEnd - current;

            if (junk > 0)
            {
                Reserve(sizeof(uint) + SeedBytes);
                BinaryPrimitives.WriteUInt32BigEndian(_output.AsSpan(_outputLength), (uint)junk | 0x80000000u);
                _seeds.AsSpan(seedIndex * SeedBytes, SeedBytes).CopyTo(_output.AsSpan(_outputLength + sizeof(uint)));

                _outputLength += sizeof(uint) + SeedBytes;
                current += junk;
                PackedSize += sizeof(uint) + SeedBytes;
            }
        }

        return true;
    }

    private void Reserve(int additional)
    {
        int required = _outputLength + additional;

        if (_output.Length >= required)
            return;

        Array.Resize(ref _output, Math.Max(required, Math.Max(_output.Length * 2, 0x4000)));
    }
}