namespace CHD.Core.Models;

public class TrackInfo
{
    public int TrackNumber { get; set; }

    public string TrackType { get; set; }

    public string SubType { get; set; }

    public int Frames { get; set; }

    public int ExtraFrames { get; set; }

    public int PadFrames { get; set; }

    public int PreGap { get; set; }

    public int PostGap { get; set; }

    public int PgType { get; set; }

    public int PgSub { get; set; }

    public int PgDataSize { get; set; }

    public int LogFrames { get; set; }

    public long LogFrameOfs { get; set; }

    public long ChdFrameOfs { get; set; }

    public long PhysFrameOfs { get; set; }

    public string GetFormattedDuration()
    {
        int totalSeconds = Frames / 75;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:D2}:{seconds:D2}";
    }
}