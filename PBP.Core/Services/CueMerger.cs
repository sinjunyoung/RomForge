using PBP.Core.Models;

namespace PBP.Core.Services;

public static class CueMerger
{
    public static (Stream Stream, CueFile MergedCue) MergeStreams(CueFile unmergedCue)
    {
        var mergedEntry = new CueFileEntry { FileType = "BINARY", Tracks = [] };
        var merged = new CueFile { Entries = [mergedEntry] };
        var streams = new List<Stream>();
        long currentFrame = 0;
        var basePath = Path.GetDirectoryName(unmergedCue.FilePath) ?? string.Empty;

        foreach (var entry in unmergedCue.Entries)
        {
            var binPath = entry.FileName;

            if (string.IsNullOrEmpty(Path.GetDirectoryName(binPath)) || binPath.StartsWith('.'))
                binPath = Path.Combine(basePath, entry.FileName);

            var srcStream = new FileStream(binPath, FileMode.Open, FileAccess.Read);
            long frameCount = srcStream.Length / 2352;
            streams.Add(srcStream);

            foreach (var track in entry.Tracks)
            {
                var newIndexes = track.Indexes
                    .Select(idx => new CueIndex { Number = idx.Number, Position = idx.Position + TocBuilder.PositionFromFrames(currentFrame) })
                    .ToList();
                mergedEntry.Tracks.Add(new CueTrack { DataType = track.DataType, Number = track.Number, Indexes = newIndexes });
            }

            currentFrame += frameCount;
        }

        return (new ConcatenatedStream(streams), merged);
    }
}