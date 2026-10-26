using Common;
using System.IO;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

internal static class IncompleteOutput
{
    public static void Delete(string? finalPath, Action<string, LogLevel, string> log, string titleId)
    {
        if (!string.IsNullOrEmpty(finalPath) && File.Exists(finalPath))
            try 
            { 
                File.Delete(finalPath); 
                log?.Invoke(Res.Log_DeleteIncompleteFile, LogLevel.Info, titleId); 
            }
            catch { }
    }
}