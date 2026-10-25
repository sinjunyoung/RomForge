namespace PBP.AT3.Models;

public sealed class UpsamplerResult
{
    public float[] Signal = [];

    public float HighFreqRatio;

    public float[] RawMagnitude = [];

    public float[] FilteredMagnitude = [];

    public float PitchPeriod;

    public float CepstralProminenceDb;
}