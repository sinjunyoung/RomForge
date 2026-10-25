namespace PSP.PBP.Models;

public class CueIndex
{
    public int Number { get; set; }

    public MsfPosition Position { get; set; } = new();
}