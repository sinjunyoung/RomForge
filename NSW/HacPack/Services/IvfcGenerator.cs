using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace NSW.HacPack.Services;

public static class IvfcGenerator
{
    public static void GenerateHashLevel(Stream outputStream, Stream inputStream, out ulong generatedSize)
    {
        const int readSize = 1 << 20;

        var builder = new IvfcLevelBuilder();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(readSize);

        try
        {
            inputStream.Position = 0;

            long remaining = inputStream.Length;

            while (remaining > 0)
            {
                int read = inputStream.Read(buffer, 0, (int)Math.Min(readSize, remaining));

                if (read <= 0)
                    throw new EndOfStreamException("IVFC 입력 스트림이 예상보다 일찍 끝났습니다.");

                builder.Add(buffer, 0, read);

                remaining -= read;
            }

            builder.Complete(outputStream, out generatedSize);
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