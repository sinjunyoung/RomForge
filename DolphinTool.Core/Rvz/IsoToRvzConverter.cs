using DolphinTool.Core.Services.GameCube;
using DolphinTool.Core.Services.Wii;
using System.Buffers.Binary;

namespace DolphinTool.Core.Rvz;

public static class IsoToRvzConverter
{
    public static void Convert(string inputPath, string outputPath, int compressionLevel = 18, int chunkSize = 131072, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var input = RvzInputSource.Open(inputPath);
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
            if (!succeeded)
            {
                try
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { }
            }
        }
    }
}