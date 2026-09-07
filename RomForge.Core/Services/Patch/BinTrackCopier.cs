using CHD.Core.Services;
using Common;
using System.IO;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Patch;

public class BinTrackCopier(Action<string, LogLevel> log)
{
    public async Task<string?> CopyBinTracksAsync(string sourcePath, string outputDir, string outputPath, List<string> copiedTrackPaths, bool moveInsteadOfCopy = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
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
            log("CUE 파일을 찾을 수 없습니다. CD 이미지가 아니거나 CUE가 누락되었을 수 있습니다.", LogLevel.Error);

            return null;
        }

        var sourceDir = Path.GetDirectoryName(cuePath)!;
        string sourceMainFileName = Path.GetFileName(sourcePath);

        var binsToCopy = referencedBins.Where(b => !string.Equals(Path.GetFileName(b), sourceMainFileName, StringComparison.OrdinalIgnoreCase)).ToList();
        int trackIndex = 0;

        foreach (var binName in binsToCopy)
        {
            ct.ThrowIfCancellationRequested();

            trackIndex++;

            string sourceBinPath = Path.Combine(sourceDir, Path.GetFileName(binName));
            string targetBinPath = Path.Combine(outputDir, Path.GetFileName(binName));

            if (!File.Exists(sourceBinPath))
            {
                log($"멀티 트랙 파일을 찾을 수 없습니다: {Path.GetFileName(sourceBinPath)}", LogLevel.Error);

                return null;
            }

            progress?.Report(new ProgressInfo { Label = $"트랙 복사 중 ({trackIndex}/{binsToCopy.Count})", Percent = binsToCopy.Count > 0 ? trackIndex * 100 / binsToCopy.Count : 100 });

            if (moveInsteadOfCopy)
                File.Move(sourceBinPath, targetBinPath, true);
            else
                File.Copy(sourceBinPath, targetBinPath, true);

            copiedTrackPaths.Add(targetBinPath);
        }

        string newBinFileName = Path.GetFileName(outputPath);
        string outputCuePath = Path.Combine(outputDir, Path.ChangeExtension(newBinFileName, ".cue"));

        try
        {
            string cueContent = await File.ReadAllTextAsync(cuePath).ConfigureAwait(false);
            string updatedCueContent = Regex.Replace(cueContent, @"FILE\s+""([^""]+)""\s+BINARY", m =>
            {
                string referencedFileName = Path.GetFileName(m.Groups[1].Value);

                return string.Equals(referencedFileName, sourceMainFileName, StringComparison.OrdinalIgnoreCase)
                    ? $"FILE \"{newBinFileName}\" BINARY"
                    : m.Value;
            }, RegexOptions.IgnoreCase);

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