using N3DS.Core.Models;
using System.Security.Cryptography;

namespace N3DS.Core.Services;

public static class ExeFsUnpacker
{
    private const int MediaUnit = 0x200;

    public static async Task<ExeFsUnpackResult> UnpackAsync(Stream ncchStream, NcchHeader ncchHeader, CancellationToken ct = default)
    {
        if (ncchHeader.ExefsOffset == 0 || ncchHeader.ExefsSize == 0)
            throw new InvalidOperationException("이 NCCH에는 ExeFS가 없습니다.");

        long exefsAbsOffset = (long)ncchHeader.ExefsOffset * MediaUnit;
        byte[] headerBuf = new byte[ExeFsHeader.Size];

        ncchStream.Position = exefsAbsOffset;

        await ncchStream.ReadExactlyAsync(headerBuf, ct);

        var header = ExeFsHeader.Parse(headerBuf);

        if (header.Entries[0].Name.Length == 0 || header.Entries[0].Offset != 0)
            throw new InvalidDataException("ExeFS 헤더가 손상되었습니다 (첫 번째 엔트리 이상).");

        long dataBase = exefsAbsOffset + ExeFsHeader.Size;
        var files = new List<ExeFsFile>();

        for (int i = 0; i < ExeFsHeader.MaxEntries; i++)
        {
            var entry = header.Entries[i];

            if (entry.Size == 0)
                continue;

            byte[] data = new byte[entry.Size];

            ncchStream.Position = dataBase + entry.Offset;

            await ncchStream.ReadExactlyAsync(data, ct);

            byte[] expectedHash = header.GetHashForEntry(i);
            byte[] actualHash = SHA256.HashData(data);
            bool hashValid = actualHash.AsSpan().SequenceEqual(expectedHash);

            files.Add(new ExeFsFile
            {
                Name = entry.Name,
                Data = data,
                ExpectedHash = expectedHash,
                HashValid = hashValid,
            });
        }

        return new ExeFsUnpackResult { Header = header, Files = files };
    }

    public static async Task SaveToDirectoryAsync(ExeFsUnpackResult result, string outputDir, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        long totalBytes = result.Files.Sum(f => (long)f.Data.Length);
        long currentBytes = 0;

        if (totalBytes > 0) reporter?.Invoke(0, totalBytes);

        foreach (var file in result.Files)
        {
            ct.ThrowIfCancellationRequested();

            string baseName = file.Name.StartsWith('.') ? file.Name[1..] : file.Name;
            string filePath = Path.Combine(outputDir, baseName + ".bin");

            await File.WriteAllBytesAsync(filePath, file.Data, ct);

            currentBytes += file.Data.Length;

            reporter?.Invoke(currentBytes, totalBytes);
        }
    }

    public static List<ExeFsFile> LoadFromDirectory(string dir)
    {
        var files = new List<ExeFsFile>();

        foreach (var path in Directory.EnumerateFiles(dir, "*.bin"))
        {
            string baseName = Path.GetFileNameWithoutExtension(path);
            string name = baseName == "code" ? ".code" : baseName;

            files.Add(new ExeFsFile
            {
                Name = name,
                Data = File.ReadAllBytes(path),
                ExpectedHash = [],
                HashValid = false,
            });
        }

        return files;
    }
}