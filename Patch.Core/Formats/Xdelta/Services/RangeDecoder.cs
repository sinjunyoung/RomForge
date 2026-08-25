using Patch.Core.Formats.Xdelta.Models;
using System.Runtime.CompilerServices;

namespace Patch.Core.Formats.Xdelta.Services;

internal ref struct RangeDecoder
{
    internal const int kNumBitModelTotalBits = 11;
    internal const uint kBitModelTotal = 1u << kNumBitModelTotalBits;
    internal const int kNumMoveBits = 5;
    private const uint kTopValue = 1u << 24;

    private uint _range;
    private uint _code;
    private ReadOnlySpan<byte> _buffer;
    private int _pos;

    public readonly int Position => _pos;

    public void Init(ReadOnlyMemory<byte> input, int offset) => Init(input.Span, offset);

    public void Init(ReadOnlySpan<byte> input, int offset)
    {
        if (input.Length - offset < 5)
            throw new LzmaDataErrorException("Truncated range coder init sequence.");

        _buffer = input;
        _pos = offset;

        if (input[_pos] != 0x00)
            throw new LzmaDataErrorException("Invalid range decoder initial byte.");

        _pos++;
        _code = 0;
        _range = 0xFFFFFFFF;

        for (int i = 0; i < 4; i++)
            _code = (_code << 8) | input[_pos++];
    }

    public void Init(ReadOnlySpan<byte> input, ref int offset)
    {
        Init(input, offset);

        offset += 5;
    }

    public void SetBuffer(ReadOnlyMemory<byte> input, int pos)
    {
        _buffer = input.Span;
        _pos = pos;
    }

    public void SetBuffer(ReadOnlySpan<byte> input, int pos)
    {
        _buffer = input;
        _pos = pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Normalize()
    {
        if (_range < kTopValue)
        {
            if ((uint)_pos >= (uint)_buffer.Length)
                throw new LzmaDataErrorException("Unexpected end of range-coded data.");

            _range <<= 8;
            _code = (_code << 8) | _buffer[_pos++];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeBit(ref ushort prob)
    {
        uint bound = (_range >> kNumBitModelTotalBits) * prob;

        if (_code < bound)
        {
            _range = bound;
            prob += (ushort)((kBitModelTotal - prob) >> kNumMoveBits);

            Normalize();

            return 0;
        }
        else
        {
            _code -= bound;
            _range -= bound;
            prob -= (ushort)(prob >> kNumMoveBits);

            Normalize();

            return 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeDirectBits(int numBits)
    {
        uint result = 0;

        for (int i = numBits; i > 0; i--)
        {
            _range >>= 1;

            uint t = (_code - _range) >> 31;

            _code -= _range & (t - 1);
            result = (result << 1) | (1 - t);

            Normalize();
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeBitTree(ushort[] probs, int probsOffset, int numBits)
    {
        uint symbol = 1;

        for (int i = 0; i < numBits; i++)
            symbol = (symbol << 1) | DecodeBit(ref probs[probsOffset + symbol]);

        return symbol - (1u << numBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeBitTree(ref ushort probsBase, int numBits)
    {
        uint symbol = 1;

        for (int i = 0; i < numBits; i++)
            symbol = (symbol << 1) | DecodeBit(ref Unsafe.Add(ref probsBase, (int)symbol));

        return symbol - (1u << numBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeReverseBitTree(ushort[] probs, int probsOffset, int numBits)
    {
        uint symbol = 1;
        uint result = 0;

        for (int i = 0; i < numBits; i++)
        {
            uint bit = DecodeBit(ref probs[probsOffset + symbol]);

            symbol = (symbol << 1) | bit;
            result |= bit << i;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint DecodeReverseBitTree(ref ushort probsBase, int numBits)
    {
        uint symbol = 1;
        uint result = 0;

        for (int i = 0; i < numBits; i++)
        {
            uint bit = DecodeBit(ref Unsafe.Add(ref probsBase, (int)symbol));

            symbol = (symbol << 1) | bit;
            result |= bit << i;
        }

        return result;
    }

    public readonly bool IsFinished => _code == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitProbs(ushort[] probs)
    {
        const ushort kProbInitValue = (ushort)(kBitModelTotal >> 1);

        Array.Fill(probs, kProbInitValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitProbs(ushort[] probs, int offset, int count)
    {
        const ushort kProbInitValue = (ushort)(kBitModelTotal >> 1);

        Array.Fill(probs, kProbInitValue, offset, count);
    }
}