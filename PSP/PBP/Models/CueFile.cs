namespace PSP.PBP.Models;

public class CueFile
{
    public string FilePath { get; set; } = string.Empty;

    public List<CueFileEntry> Entries { get; set; } = [];
}