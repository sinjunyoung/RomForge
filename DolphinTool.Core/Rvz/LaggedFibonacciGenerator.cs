using System.Buffers.Binary;

namespace DolphinTool.Core.Rvz;

internal sealed class LaggedFibonacciGenerator
{
    public const int SeedBytes = 17 * sizeof(uint);

    private const int SeedSize = 17;
    private const int K = 521;
    private const int J = 32;
    private const int BufferBytes = K * sizeof(uint);

    private readonly uint[] _words = new uint[K];
    private readonly byte[] _bytes = new byte[BufferBytes];
    private int _positionBytes;
    private bool _dirty;

    public void SetSeed(ReadOnlySpan<byte> seed)
    {
        _positionBytes = 0;

        for (int i = 0; i < SeedSize; i++)
            _words[i] = BinaryPrimitives.ReadUInt32BigEndian(seed[(i * sizeof(uint))..]);

        Initialize(false);
    }

    public void Forward(long count)
    {
        _positionBytes += (int)count;

        while (_positionBytes >= BufferBytes)
        {
            Step();

            _positionBytes -= BufferBytes;
        }

        _dirty = true;
    }

    public void GetBytes(Span<byte> destination)
    {
        if (_dirty)
            Refresh();

        int written = 0;

        while (written < destination.Length)
        {
            int length = Math.Min(destination.Length - written, BufferBytes - _positionBytes);

            _bytes.AsSpan(_positionBytes, length).CopyTo(destination[written..]);

            _positionBytes += length;
            written += length;

            if (_positionBytes == BufferBytes)
            {
                Step();
                Refresh();

                _positionBytes = 0;
            }
        }
    }

    public int GetSeed(ReadOnlySpan<byte> data, int dataOffset, Span<byte> seedOut)
    {
        int bytesToSkip = ((dataOffset + 3) & ~3) - dataOffset;

        if (data.Length < bytesToSkip)
            return 0;

        var wordData = data[bytesToSkip..];
        int wordCount = wordData.Length / sizeof(uint);
        int wordOffset = (dataOffset + bytesToSkip) / sizeof(uint);

        if (!TryReconstruct(wordData, wordCount, wordOffset, seedOut))
            return 0;

        _positionBytes = dataOffset % BufferBytes;

        int reconstructed = 0;

        while (reconstructed < data.Length && GetByte() == data[reconstructed])
            reconstructed++;

        return reconstructed;
    }

    private bool TryReconstruct(ReadOnlySpan<byte> data, int wordCount, int wordOffset, Span<byte> seedOut)
    {
        if (wordCount < K)
            return false;

        for (int i = 0; i < K; i++)
        {
            uint value = BinaryPrimitives.ReadUInt32BigEndian(data[(i * sizeof(uint))..]);

            if ((value & 0x00C00000u) != ((value >> 2) & 0x00C00000u))
                return false;
        }

        int offsetModK = wordOffset % K;
        int offsetDivK = wordOffset / K;

        for (int i = 0; i < K - offsetModK; i++)
            _words[offsetModK + i] = BinaryPrimitives.ReadUInt32BigEndian(data[(i * sizeof(uint))..]);

        for (int i = 0; i < offsetModK; i++)
            _words[i] = BinaryPrimitives.ReadUInt32BigEndian(data[((K - offsetModK + i) * sizeof(uint))..]);

        Backward(0, offsetModK);

        for (int i = 0; i < offsetDivK; i++)
            Backward(0, K);

        if (!Reinitialize(seedOut))
            return false;

        for (int i = 0; i < offsetDivK; i++)
            Step();

        _dirty = true;

        return true;
    }

    private bool Reinitialize(Span<byte> seedOut)
    {
        for (int i = 0; i < 4; i++)
            Backward(0, K);

        for (int i = 0; i < SeedSize; i++)
        {
            uint x = _words[i];

            _words[i] = (x & 0xFF00FFFFu) | ((x << 2) & 0x00FC0000u) | (((_words[i + 16] ^ _words[i + 15]) << 9) & 0x00030000u);
        }

        for (int i = 0; i < SeedSize; i++)
            BinaryPrimitives.WriteUInt32BigEndian(seedOut[(i * sizeof(uint))..], _words[i]);

        return Initialize(true);
    }

    private bool Initialize(bool checkExistingData)
    {
        for (int i = SeedSize; i < K; i++)
        {
            uint calculated = (_words[i - 17] << 23) ^ (_words[i - 16] >> 9) ^ _words[i - 1];

            if (checkExistingData)
            {
                uint actual = (_words[i] & 0xFF00FFFFu) | ((_words[i] << 2) & 0x00FC0000u);

                if ((calculated & 0xFFFCFFFFu) != actual)
                    return false;
            }

            _words[i] = calculated;
        }

        for (int i = 0; i < K; i++)
        {
            uint x = _words[i];

            _words[i] = (x & 0xFF00FFFFu) | ((x >> 2) & 0x00FF0000u);
        }

        for (int i = 0; i < 4; i++)
            Step();

        _dirty = true;

        return true;
    }

    private byte GetByte()
    {
        if (_dirty)
            Refresh();

        byte result = _bytes[_positionBytes];

        _positionBytes++;

        if (_positionBytes == BufferBytes)
        {
            Step();
            Refresh();
            _positionBytes = 0;
        }

        return result;
    }

    private void Step()
    {
        for (int i = 0; i < J; i++)
            _words[i] ^= _words[i + K - J];

        for (int i = J; i < K; i++)
            _words[i] ^= _words[i - J];
    }

    private void Backward(int startWord, int endWord)
    {
        int loopEnd = Math.Max(J, startWord);

        for (int i = Math.Min(endWord, K); i > loopEnd; i--)
            _words[i - 1] ^= _words[i - 1 - J];

        for (int i = Math.Min(endWord, J); i > startWord; i--)
            _words[i - 1] ^= _words[i - 1 + K - J];
    }

    private void Refresh()
    {
        for (int i = 0; i < K; i++)
            BinaryPrimitives.WriteUInt32BigEndian(_bytes.AsSpan(i * sizeof(uint)), _words[i]);

        _dirty = false;
    }
}