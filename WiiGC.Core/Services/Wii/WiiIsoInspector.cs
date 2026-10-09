using System.Text;

namespace WiiGC.Core.Services.Wii;

public static class WiiIsoInspector
{
    public static void Dump(string isoPath, string outputPath)
    {
        using var input = RvzInputSource.Open(isoPath);

        if (input.Length < 0x20)
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        byte[] header = new byte[0x20];

        input.Read(0, header);

        if (!DiscHeader.IsWii(header))
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        var specs = WiiPartitionTable.Read(input, input.Length);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));

        writer.WriteLine($"source: {isoPath}");
        writer.WriteLine($"length: {input.Length}");
        writer.WriteLine($"partitions: {specs.Count}");

        for (int p = 0; p < specs.Count; p++)
        {
            var spec = specs[p];

            writer.WriteLine();
            writer.WriteLine($"[partition {p}] container=0x{spec.ContainerOffset:X} data=0x{spec.DataStart:X} size={spec.DataSize}");

            try
            {
                using var reader = new WiiPartitionReader(input, spec);
                var info = WiiFileSystem.Read(reader);

                writer.WriteLine($"gameId: {info.GameId}");
                writer.WriteLine($"title: {info.Title}");
                writer.WriteLine($"dol: 0x{info.DolOffset:X}");
                writer.WriteLine($"fst: offset=0x{info.FstOffset:X} size={info.FstSize}");
                writer.WriteLine($"files: {info.Files.Count}");
                writer.WriteLine();

                foreach (var file in info.Files)
                    writer.WriteLine($"{file.Offset:X10} {file.Size,12} {file.Path}");
            }
            catch (Exception ex)
            {
                writer.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}