using System.Buffers.Binary;
using WiiGC.Core.Services.GameCube;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

public static class IsoToRvzConverter
{
    public static void Convert(string inputPath, string outputPath, int compressionLevel = 18, int chunkSize = 131072, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);

        Convert(input, outputPath, compressionLevel, chunkSize, progress, ct);
    }

    internal static void Convert(IRvzInputSource input, string outputPath, int compressionLevel = 18, int chunkSize = 131072, Action<double>? progress = null, CancellationToken ct = default)
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIo);
        ThreadPool.SetMinThreads(Math.Max(minWorker, Environment.ProcessorCount * 3), minIo);

        bool succeeded = false;

        try
        {
            using var output = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
            Span<byte> header = stackalloc byte[0x20];

            if (input.Length < header.Length)
                throw new InvalidDataException("디스크 이미지가 너무 작습니다.");

            input.Read(0, header);

            if (RvzWiiWriter.IsWii(header))
            {
                var writer = new RvzWiiWriter(input, output, compressionLevel, chunkSize);

                writer.Write(progress, ct);
            }
            else if (BinaryPrimitives.ReadUInt32BigEndian(header[0x1C..]) == 0xC2339F3D)
            {
                var writer = new RvzGcWriter(input, output, compressionLevel, chunkSize);

                writer.Write(progress, ct);
            }
            else
                throw new InvalidDataException("GameCube 또는 Wii 디스크 이미지가 아닙니다.");

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }
    }
}