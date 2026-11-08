using NSW.Core.Models;
using Common.Localization;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal sealed record ConvertProfile(string StartLog, string? CopyStatus, string DisplayNamePrefix, bool DropXciUpdatePartition, bool LogCompressionRatio, string ValidationStart, string ValidationEnd, Func<MetadataResult, string, string> ValidationLabel, Func<string, string> DoneLog)
{
    public static ConvertProfile ForCompression(string inputPath, bool isCompressMode)
    {
        string modeDone = isCompressMode ? Loc.S.LogModeCompress : Loc.S.LogModeDecompress;

        return new ConvertProfile($"{Path.GetFileName(inputPath)} {modeDone} {Loc.S.LogProcessStart}", isCompressMode ? Loc.S.LogStatusCopying : Loc.S.LogStatusDecompressing, $"{(isCompressMode ? Loc.S.LogStatusCompressing : Loc.S.LogStatusDecompressing)} ", isCompressMode, isCompressMode,
            Loc.S.LogStatusValidating, Loc.S.LogValidationComplete, (meta, _) => $"{meta.KrTitle ?? meta.EnTitle} [{meta.TitleId}]", finalPath => $"{modeDone} {Loc.S.LogStatusDone}: {Path.GetFileName(finalPath)}");
    }

    public static ConvertProfile ForConversion(string inputPath, OutputContainer output, bool useCompression)
    {
        return new ConvertProfile($"{Path.GetFileName(inputPath)} → {output.Extension(useCompression).TrimStart('.').ToUpper()} 변환 시작", null, string.Empty, false, false,
            Loc.S.ToolTipValidateCompress, "OK", (meta, entryName) => $"{meta.KrTitle ?? meta.EnTitle} [{entryName}]", finalPath => $"변환 완료: {Path.GetFileName(finalPath)}");
    }
}