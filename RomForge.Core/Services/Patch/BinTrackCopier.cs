using CHD.Core.Services;
using Common;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public class BinTrackCopier(Action<string, LogLevel> log)
{
    public async Task<string?> CopyBinTracksAsync(string sourcePath, string outputDir, string outputPath, List<string> copiedTrackPaths)
    {
        string[] cueCandidates = Directory.GetFiles(Path.GetDirectoryName(sourcePath)!, "*.cue");

        string? cuePath = null;
        IReadOnlyList<string>? referencedBins = null;

        foreach (var candidate in cueCandidates)
        {
            var bins = ConversionSource.ParseBinsFromCue(candidate);

            if (bins.Any(b => string.Equals(Path.GetFileName(b), Path.GetFileName(sourcePath), StringComparison.OrdinalIgnoreCase)))
            {
                cuePath = candidate;
                referencedBins = bins;

                break;
            }
        }

        if (cuePath is null || referencedBins is null)
        {
            log("CUE 파일을 찾을 수 없습니다.", LogLevel.Error);

            return null;
        }

        var sourceDir = Path.GetDirectoryName(cuePath)!;
        string sourceMainFileName = Path.GetFileName(sourcePath);

        foreach (var binName in referencedBins)
        {
            if (string.Equals(Path.GetFileName(binName), sourceMainFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            string sourceBinPath = Path.Combine(sourceDir, Path.GetFileName(binName));
            string targetBinPath = Path.Combine(outputDir, Path.GetFileName(binName));

            if (File.Exists(sourceBinPath))
            {
                File.Copy(sourceBinPath, targetBinPath, true);
                copiedTrackPaths.Add(targetBinPath);
            }
            else
            {
                log($"멀티 트랙 파일을 찾을 수 없습니다: {Path.GetFileName(sourceBinPath)}", LogLevel.Error);

                return null;
            }
        }

        string newBinFileName = Path.GetFileName(outputPath);
        string outputCuePath = Path.Combine(outputDir, Path.ChangeExtension(newBinFileName, ".cue"));

        try
        {
            string cueContent = await File.ReadAllTextAsync(cuePath).ConfigureAwait(false);
            string updatedCueContent = Regex.Replace(cueContent, @"FILE\s+""([^""]+)""\s+BINARY", $"FILE \"{newBinFileName}\" BINARY", RegexOptions.IgnoreCase);

            await File.WriteAllTextAsync(outputCuePath, updatedCueContent).ConfigureAwait(false);

            return outputCuePath;
        }
        catch (Exception ex)
        {
            log($"CUE 파일 처리 중 오류 발생: {ex.Message}", LogLevel.Error);

            return null;
        }
    }
}