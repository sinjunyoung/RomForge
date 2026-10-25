using CD.Core.Models;
using Common;

namespace CD.Core.Services.Writers;

public static class BinCueWriter
{
    private const int OutputSectorSize = 2352;

    public static async Task<string> WriteAsync(DiscImage image, string outputDir, string outputBaseName, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        var baseName = outputBaseName;
        string cuePath, binPath;

        while (true)
        {
            cuePath = Path.Combine(outputDir, $"{baseName}.cue");
            binPath = Path.Combine(outputDir, $"{baseName}.bin");

            if (!File.Exists(cuePath) && !File.Exists(binPath))
                break;

            baseName += "_";
        }

        var binFileName = Path.GetFileName(binPath);
        var totalSectors = image.Tracks.Sum(t => (long)t.TotalSectors);
        long sectorsDone = 0;
        var cueEntry = new CueFileEntry { FileName = binFileName, FileType = "BINARY", Tracks = [] };
        long currentFrame = 0;

        await using (var outStream = new FileStream(binPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        {
            foreach (var track in image.Tracks)
            {
                ct.ThrowIfCancellationRequested();

                await using var src = track.OpenSectorStream();

                await SectorStreamCopier.CopySectorsAsync(src, outStream, track.TotalSectors, track.SourceSectorSize, OutputSectorSize,
                    count =>
                    {
                        sectorsDone += count;

                        progress?.Report(new ProgressInfo((int)(totalSectors > 0 ? sectorsDone * 100 / totalSectors : 100), "BIN/CUE 변환 중", outputBaseName, string.Empty, string.Empty));
                    },
                    ct);

                var indexes = new List<CueIndex>();

                if (track.PregapSectors > 0)
                    indexes.Add(new CueIndex { Number = 0, Position = FramesToPosition(currentFrame) });

                indexes.Add(new CueIndex { Number = 1, Position = FramesToPosition(currentFrame + track.PregapSectors) });

                cueEntry.Tracks.Add(new CueTrack
                {
                    Number = track.Number,
                    DataType = track.DataType,
                    Indexes = indexes
                });

                currentFrame += track.TotalSectors;
            }
        }

        var cueFile = new CueFile { FilePath = cuePath, Entries = [cueEntry] };

        CueFileWriter.Write(cueFile, cuePath);

        return cuePath;
    }

    private static MsfPosition FramesToPosition(long frames)
    {
        var totalSeconds = (int)(frames / 75);

        return new MsfPosition { Minutes = totalSeconds / 60, Seconds = totalSeconds % 60, Frames = (int)(frames % 75) };
    }
}