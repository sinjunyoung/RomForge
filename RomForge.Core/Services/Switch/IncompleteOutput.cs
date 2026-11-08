using Common.Models;
using Common.Localization;
using System.IO;

namespace RomForge.Core.Services.Switch;

internal static class IncompleteOutput
{
    public static void Delete(string? finalPath, Action<string, LogLevel, string?>? log, string? titleId)
    {
        if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
            try 
            { 
                File.Delete(finalPath); 
                log?.Invoke(Loc.S.LogDeleteIncompleteFile, LogLevel.Info, titleId); 
            }
            catch { }
    }
}