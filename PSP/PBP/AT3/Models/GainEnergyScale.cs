namespace PSP.PBP.AT3.Models;

public struct GainEnergyScale
{
    public float PrevHalf;

    public float CurHalf;

    public float Frame;

    public static GainEnergyScale Identity => new() { PrevHalf = 1.0f, CurHalf = 1.0f, Frame = 1.0f };
}