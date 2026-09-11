using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaLicenseInstaller
{
    public static string Install(WorkBinLicense license, string workBinPath, string outputRoot)
    {
        if (!WorkBinReader.TryGetTitleIdFromContentId(license.ContentId, out string titleId))
            throw new InvalidDataException($"content_id에서 title id를 추출할 수 없습니다: {license.ContentId}");

        string licenseDir = Path.Combine(outputRoot, "license", titleId);

        Directory.CreateDirectory(licenseDir);

        string rifPath = Path.Combine(licenseDir, $"{license.ContentId}.rif");

        File.Copy(workBinPath, rifPath, overwrite: true);

        return rifPath;
    }
}