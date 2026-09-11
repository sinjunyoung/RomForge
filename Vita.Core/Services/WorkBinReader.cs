using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class WorkBinReader
{
    public static WorkBinLicense Read(string workBinPath)
    {
        byte[] data = File.ReadAllBytes(workBinPath);

        if (data.Length < WorkBinLicense.MinSize)
            throw new InvalidDataException($"work.bin 크기가 너무 작습니다: {workBinPath}");

        string contentId = System.Text.Encoding.ASCII
            .GetString(data, WorkBinLicense.ContentIdOffset, WorkBinLicense.ContentIdSize)
            .TrimEnd('\0');

        byte[] klicensee = data
            .AsSpan(WorkBinLicense.KlicenseeOffset, WorkBinLicense.KlicenseeSize)
            .ToArray();

        if (klicensee.All(b => b == 0))
            throw new InvalidDataException($"work.bin에 유효한 klicensee가 없습니다: {workBinPath}");

        return new WorkBinLicense
        {
            ContentId = contentId,
            Klicensee = klicensee
        };
    }

    public static string? FindMatchingWorkBin(string workBinSearchDir, string contentId)
    {
        if (!Directory.Exists(workBinSearchDir))
            return null;

        foreach (var candidate in Directory.EnumerateFiles(workBinSearchDir, "*.bin", SearchOption.AllDirectories))
        {
            try
            {
                var license = WorkBinReader.Read(candidate);

                if (license.ContentId.Equals(contentId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            catch (InvalidDataException)
            {
            }
        }

        return null;
    }
}