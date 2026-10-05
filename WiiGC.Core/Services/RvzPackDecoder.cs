using System.Buffers.Binary;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal static class RvzPackDecoder
{
    public static void Unpack(ReadOnlySpan<byte> packed, Span<byte> destination, long dataOffset, LaggedFibonacciGenerator generator)
    {
        int inputPosition = 0;
        int outputPosition = 0;

        while (inputPosition < packed.Length)
        {
            if (packed.Length - inputPosition < sizeof(uint))
                throw new InvalidDataException("RVZ 패킹 데이터가 잘못되었습니다.");

            uint header = BinaryPrimitives.ReadUInt32BigEndian(packed[inputPosition..]);

            inputPosition += sizeof(uint);

            bool junk = (header & 0x80000000u) != 0;
            int size = (int)(header & 0x7FFFFFFFu);

            if (size > destination.Length - outputPosition)
                throw new InvalidDataException("RVZ 패킹 해제 결과가 예상보다 큽니다.");

            if (junk)
            {
                if (packed.Length - inputPosition < LaggedFibonacciGenerator.SeedBytes)
                    throw new InvalidDataException("RVZ 패킹 시드 데이터가 잘못되었습니다.");

                generator.SetSeed(packed.Slice(inputPosition, LaggedFibonacciGenerator.SeedBytes));

                inputPosition += LaggedFibonacciGenerator.SeedBytes;

                generator.Forward((dataOffset + outputPosition) % WiiLayout.LfgBlockSize);
                generator.GetBytes(destination.Slice(outputPosition, size));
            }
            else
            {
                if (packed.Length - inputPosition < size)
                    throw new InvalidDataException("RVZ 패킹 데이터가 잘못되었습니다.");

                packed.Slice(inputPosition, size).CopyTo(destination[outputPosition..]);

                inputPosition += size;
            }

            outputPosition += size;
        }

        if (outputPosition != destination.Length)
            throw new InvalidDataException("RVZ 패킹 해제 결과가 예상보다 작습니다.");
    }
}