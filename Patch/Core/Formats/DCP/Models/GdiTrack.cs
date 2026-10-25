namespace Patch.Core.Formats.DCP.Models;

public class GdiTrack
{
    public int Number { get; init; }

    public long StartLba { get; init; }

    public TrackType Type { get; init; }

    public int SectorSize { get; init; }

    public string FileName { get; init; } = string.Empty;

    public long FileOffset { get; init; }
}