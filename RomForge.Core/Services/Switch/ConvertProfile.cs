using NSW.Core.Models;
using Path = System.IO.Path;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

internal sealed record ConvertProfile(string StartLog, string? CopyStatus, string DisplayNamePrefix, bool DropXciUpdatePartition, bool LogCompressionRatio, string ValidationStart, string ValidationEnd, Func<MetadataResult, string, string> ValidationLabel, Func<string, string> DoneLog)
{
    public static ConvertProfile ForCompression(string inputPath, bool isCompressMode)
    {
        string modeDone = isCompressMode ? Res.Log_ModeCompress : Res.Log_ModeDecompress;

        return new ConvertProfile($"{Path.GetFileName(inputPath)} {modeDone} {Res.Log_ProcessStart}", isCompressMode ? Res.Log_StatusCopying : Res.Log_StatusDecompressing, $"{(isCompressMode ? Res.Log_StatusCompressing : Res.Log_StatusDecompressing)} ", isCompressMode, isCompressMode,
            Res.Log_StatusValidating, Res.Log_ValidationComplete, (meta, _) => $"{meta.KrTitle ?? meta.EnTitle} [{meta.TitleId}]", finalPath => $"{modeDone} {Res.Log_StatusDone}: {Path.GetFileName(finalPath)}");
    }

    public static ConvertProfile ForConversion(string inputPath, OutputContainer output, bool useCompression)
    {
        return new ConvertProfile($"{Path.GetFileName(inputPath)} → {output.Extension(useCompression).TrimStart('.').ToUpper()} 변환 시작", null, string.Empty, false, false,
            Res.ToolTip_ValidateCompress, "OK", (meta, entryName) => $"{meta.KrTitle ?? meta.EnTitle} [{entryName}]", finalPath => $"변환 완료: {Path.GetFileName(finalPath)}");
    }
}