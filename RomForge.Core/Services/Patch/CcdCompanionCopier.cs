using Common;
using System.IO;

namespace RomForge.Core.Services.Patch;

public class CcdCompanionCopier(Action<string, LogLevel>? log)
{
    public string? CopyCcd(string? sourcePath, string outputPath, bool moveInsteadOfCopy = false)
    {
        if (sourcePath == null)
            throw new NullReferenceException();

        string sourceCcdPath = Path.ChangeExtension(sourcePath, ".ccd");

        if (!File.Exists(sourceCcdPath))
        {
            log?.Invoke("CCD 파일을 찾을 수 없습니다. CD 이미지가 아니거나 CCD가 누락되었을 수 있습니다.", LogLevel.Error);
            return null;
        }

        string outputCcdPath = Path.ChangeExtension(outputPath, ".ccd");

        try
        {
            if (moveInsteadOfCopy)
                File.Move(sourceCcdPath, outputCcdPath, true);
            else
                File.Copy(sourceCcdPath, outputCcdPath, true);

            string sourceSubPath = Path.ChangeExtension(sourcePath, ".sub");
            string outputSubPath = Path.ChangeExtension(outputPath, ".sub");

            if (File.Exists(sourceSubPath))
            {
                if (moveInsteadOfCopy)
                    File.Move(sourceSubPath, outputSubPath, true);
                else
                    File.Copy(sourceSubPath, outputSubPath, true);
            }

            return outputCcdPath;
        }
        catch (Exception ex)
        {
            log?.Invoke($"CCD 및 동반 파일 복사 중 오류 발생: {ex.Message}", LogLevel.Error);
            return null;
        }
    }
}