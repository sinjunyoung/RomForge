namespace PBP.Core.Models;

public class CueFileEntry
{
    public string FileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public List<CueTrack> Tracks { get; set; } = [];
}