using System.IO;
using WiiGC.Core.Services;

namespace WiiGC.Core.Services.Wii;

public static class WiiIsoUnpacker
{
    private const int CopyChunkSize = 0x100000;

    public static int Unpack(string inputPath, string outputFolder, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);

        var target = WiiIsoRebuilder.FindTargets(input, WiiIsoRebuilder.ReadSpecs(input)).FirstOrDefault() ?? throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");

        using var reader = new WiiPartitionReader(input, target);

        var files = WiiFileSystem.Read(reader).Files;
        string root = Path.GetFullPath(outputFolder);
        string boundary = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var reporter = new ProgressReporter(files.Sum(f => f.Size), progress);
        byte[] buffer = new byte[CopyChunkSize];

        Directory.CreateDirectory(root);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            string destination = Path.GetFullPath(Path.Combine(root, file.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            if (!destination.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"디스크에 안전하지 않은 경로가 있습니다: {file.Path}");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, CopyChunkSize);

            long offset = file.Offset;
            long remaining = file.Size;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                int size = (int)Math.Min(buffer.Length, remaining);
                var span = buffer.AsSpan(0, size);

                reader.Read(offset, span);
                output.Write(span);

                offset += size;
                remaining -= size;

                reporter.Add(size);
            }
        }

        return files.Count;
    }
}