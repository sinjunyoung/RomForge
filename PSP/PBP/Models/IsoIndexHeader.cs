namespace PSP.PBP.Models;

public class IsoIndexHeader
{
    public uint Offset { get; set; }

    public uint Length { get; set; }

    public uint[] Dummy { get; set; } = new uint[6];
}