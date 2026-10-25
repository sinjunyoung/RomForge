namespace PBP.AT3.Models;

public struct GainCurvePoint(uint level, uint location)
{
    public uint Level = level;

    public uint Location = location;
}