using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class WorkBinReader
{
    public const string RelativePath = "sce_sys/package/work.bin";

    public static WorkBinLicense Read(string workBinPath) => Parse(File.ReadAllBytes(workBinPath), workBinPath);

    public static WorkBinLicense Read(IVitaSourceAccessor accessor, string workBinRelPath) => Parse(accessor.ReadAllBytes(workBinRelPath), workBinRelPath);

    public static WorkBinLicense ReadFromTitlePath(string titleIdPath)
    {
        string workBinPath = Path.Combine(titleIdPath, "sce_sys", "package", "work.bin");

        if (!File.Exists(workBinPath))
            throw new FileNotFoundException($"work.bin이 고정 경로에 없습니다: {workBinPath}", workBinPath);

        return Read(workBinPath);
    }

    private static WorkBinLicense Parse(byte[] data, string label)
    {
        if (data.Length < WorkBinLicense.MinSize)
            throw new InvalidDataException($"work.bin 크기가 너무 작습니다: {label}");

        string contentId = System.Text.Encoding.ASCII
            .GetString(data, WorkBinLicense.ContentIdOffset, WorkBinLicense.ContentIdSize)
            .TrimEnd('\0');
        byte[] klicensee = data
            .AsSpan(WorkBinLicense.KlicenseeOffset, WorkBinLicense.KlicenseeSize)
            .ToArray();

        if (klicensee.All(b => b == 0))
            throw new InvalidDataException($"work.bin에 유효한 klicensee가 없습니다: {label}");

        return new WorkBinLicense
        {
            ContentId = contentId,
            Klicensee = klicensee
        };
    }

    public static bool TryGetTitleIdFromContentId(string contentId, out string titleId)
    {
        if (contentId.Length >= 16)
        {
            titleId = contentId.Substring(7, 9);

            return true;
        }

        titleId = string.Empty;

        return false;
    }
}