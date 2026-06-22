using PBP.Core.Models;

namespace PBP.Core.Services;

public static class CueMerger
{
    public static CueFile MergeBins(Stream outputStream, CueFile unmergedCue)
    {
        var mergedEntry = new CueFileEntry { FileType = "BINARY", Tracks = [] };
        var merged = new CueFile { FileEntries = [mergedEntry] };
        long currentFrame = 0;
        var basePath = Path.GetDirectoryName(unmergedCue.Path) ?? string.Empty;

        foreach (var entry in unmergedCue.FileEntries)
        {
            var binPath = entry.FileName;
            var binDirectory = Path.GetDirectoryName(binPath) ?? string.Empty;

            if (binDirectory == string.Empty || binDirectory.StartsWith("..") || binDirectory.StartsWith('.'))
                binPath = Path.Combine(basePath, entry.FileName);

            using var srcStream = new FileStream(binPath, FileMode.Open, FileAccess.Read);

            srcStream.CopyTo(outputStream);

            foreach (var track in entry.Tracks)
            {
                var newIndexes = track.Indexes
                    .Select(idx => new CueIndex { Number = idx.Number, Position = idx.Position + TocBuilder.PositionFromFrames(currentFrame) })
                    .ToList();

                mergedEntry.Tracks.Add(new CueTrack { DataType = track.DataType, Number = track.Number, Indexes = newIndexes });
            }

            currentFrame += srcStream.Length / 2352;
        }

        return merged;
    }
}