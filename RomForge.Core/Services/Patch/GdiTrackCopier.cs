using Common;
using Patch.Core.Formats.DCP.Services;
using System.IO;
using System.Text;

namespace RomForge.Core.Services.Patch;

public class GdiTrackCopier(Action<string, LogLevel> log)
{
    public string? CopyGdiTracks(string sourcePath, string outputDir, string outputPath, List<string> copiedTrackPaths, bool moveInsteadOfCopy = false, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string[] gdiCandidates = Directory.GetFiles(Path.GetDirectoryName(sourcePath)!, "*.gdi");

        GdiFile? gdi = null;
        string sourceMainFileName = Path.GetFileName(sourcePath);

        foreach (var candidate in gdiCandidates)
        {
            try
            {
                var parsed = GdiFile.Parse(candidate);

                if (parsed.Tracks.Any(t => string.Equals(t.FileName, sourceMainFileName, StringComparison.OrdinalIgnoreCase)))
                {
                    gdi = parsed;

                    break;
                }
            }
            catch { }
        }

        if (gdi is null)
        {
            log("GDI 파일을 찾을 수 없습니다. GD-ROM 이미지가 아니거나 GDI가 누락되었을 수 있습니다.", LogLevel.Error);

            return null;
        }

        var tracksToCopy = gdi.Tracks.Where(t => !string.Equals(t.FileName, sourceMainFileName, StringComparison.OrdinalIgnoreCase)).ToList();
        int trackIndex = 0;

        foreach (var track in tracksToCopy)
        {
            ct.ThrowIfCancellationRequested();

            trackIndex++;

            string sourceTrackPath = gdi.GetTrackFullPath(track);
            string targetTrackPath = Path.Combine(outputDir, track.FileName);

            if (!File.Exists(sourceTrackPath))
            {
                log($"트랙 파일을 찾을 수 없습니다: {track.FileName}", LogLevel.Error);

                return null;
            }

            progress?.Report(new ProgressInfo { Label = $"트랙 복사 중 ({trackIndex}/{tracksToCopy.Count})", Percent = tracksToCopy.Count > 0 ? trackIndex * 100 / tracksToCopy.Count : 100 });

            if (moveInsteadOfCopy)
                File.Move(sourceTrackPath, targetTrackPath, true);
            else
                File.Copy(sourceTrackPath, targetTrackPath, true);

            copiedTrackPaths.Add(targetTrackPath);
        }

        string newMainFileName = Path.GetFileName(outputPath);
        string outputGdiPath = Path.Combine(outputDir, Path.ChangeExtension(newMainFileName, ".gdi"));

        try
        {
            var sb = new StringBuilder();

            sb.AppendLine(gdi.Tracks.Count.ToString());

            foreach (var track in gdi.Tracks)
            {
                string fileName = string.Equals(track.FileName, sourceMainFileName, StringComparison.OrdinalIgnoreCase)
                    ? newMainFileName
                    : track.FileName;

                string quotedFileName = fileName.Contains(' ') ? $"\"{fileName}\"" : fileName;

                sb.AppendLine($"{track.Number} {track.StartLba} {(int)track.Type} {track.SectorSize} {quotedFileName} {track.FileOffset}");
            }

            File.WriteAllText(outputGdiPath, sb.ToString());

            return outputGdiPath;
        }
        catch (Exception ex)
        {
            log($"GDI 파일 처리 중 오류 발생: {ex.Message}", LogLevel.Error);

            return null;
        }
    }
}