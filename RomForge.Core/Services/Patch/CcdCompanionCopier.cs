using Common.Localization;
using Common.Models;
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
            log?.Invoke(Loc.S.LogCcdNotFoundNotCdImage, LogLevel.Error);
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
            log?.Invoke(Loc.S.LogCcdCompanionCopyError(ex.Message), LogLevel.Error);
            return null;
        }
    }
}