using PBP.Core.Enums;

namespace PBP.Core.Models;

public class TocEntry
{
    public TrackType TrackType { get; set; }

    public int TrackNo { get; set; }

    public int Minutes { get; set; }

    public int Seconds { get; set; }

    public int Frames { get; set; }

    public int ToLBA() => (Minutes * 60 + Seconds) * 75 + Frames;
}