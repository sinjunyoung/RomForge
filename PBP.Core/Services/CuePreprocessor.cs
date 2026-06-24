using PBP.Core.Models;
namespace PBP.Core.Services;

public static class CuePreprocessor
{
    public static ResolvedDisc Resolve(string inputPath, string? tempDir = null)
    {
        if (!Path.GetExtension(inputPath).Equals(".cue", StringComparison.InvariantCultureIgnoreCase))
        {
            var size = (uint)new FileInfo(inputPath).Length;

            return ResolvedDisc.Create(new FileStream(inputPath, FileMode.Open, FileAccess.Read), size, TocBuilder.BuildSingleTrackToc(size));
        }

        var cueFile = CueFileReader.Read(inputPath);

        if (cueFile.Entries.Count > 1)
        {
            var (stream, merged) = CueMerger.MergeStreams(cueFile);
            var size = (uint)stream.Length;

            return ResolvedDisc.Create(stream, size, TocBuilder.BuildToc(merged, size));
        }

        var binPath = CueFileResolver.GetBinPath(inputPath);
        var binSize = (uint)new FileInfo(binPath).Length;

        return ResolvedDisc.Create(new FileStream(binPath, FileMode.Open, FileAccess.Read), binSize, TocBuilder.BuildToc(cueFile, binSize));
    }
}