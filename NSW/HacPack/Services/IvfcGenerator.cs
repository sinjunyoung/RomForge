using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using NSW.HacPack.Models;

namespace NSW.HacPack.Services;

public static class IvfcGenerator
{
    public static unsafe void GenerateHashLevel(Stream outputStream, Stream inputStream, out ulong generatedSize)
    {
        const int hashBlockSize = IvfcConstants.HashBlockSize;
        const int hashSize = 32;
        const int batchCount = 128;

        long srcLength = inputStream.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(hashBlockSize);
        Span<byte> outputBatch = stackalloc byte[hashSize * batchCount];
        int batchIdx = 0;

        try
        {
            inputStream.Position = 0;

            long remaining = srcLength;

            while (remaining > 0)
            {
                int toRead = (int)Math.Min((long)hashBlockSize, remaining);
                int read = inputStream.Read(buffer, 0, toRead);

                SHA256.HashData(buffer.AsSpan(0, read), outputBatch.Slice(batchIdx * hashSize, hashSize));

                batchIdx++;

                if (batchIdx == batchCount)
                {
                    outputStream.Write(outputBatch);

                    batchIdx = 0;
                }

                remaining -= read;
            }

            if (batchIdx > 0)
                outputStream.Write(outputBatch[..(batchIdx * hashSize)]);

            long currentPos = outputStream.Position;
            int paddingSize = (int)(hashBlockSize - (currentPos % hashBlockSize));

            if (paddingSize != hashBlockSize)
            {
                Array.Clear(buffer, 0, paddingSize);
                outputStream.Write(buffer, 0, paddingSize);
            }

            generatedSize = (ulong)outputStream.Position;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] CalculateMasterHash(Stream ivfcLevel1Stream)
    {
        ivfcLevel1Stream.Position = 0;
        return SHA256.HashData(ivfcLevel1Stream);
    }
}