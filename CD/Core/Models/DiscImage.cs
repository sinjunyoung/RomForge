namespace CD.Core.Models;

public class DiscImage
{
    public List<DiscTrack> Tracks { get; set; } = [];

    public int TrackCount => Tracks.Count;

    public bool IsSingleTrack => TrackCount == 1;
}