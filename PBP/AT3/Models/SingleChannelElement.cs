namespace PBP.AT3.Models;

public sealed class SingleChannelElement
{
    public SubbandInfo SubbandInfo = new();

    public List<TonalBlock> TonalBlocks = [];

    public List<ScaledBlock> ScaledBlocks = [];

    public float Loudness;

    public GainEnergyScale[] GainEnergyScale =
    [
        Models.GainEnergyScale.Identity, Models.GainEnergyScale.Identity,
        Models.GainEnergyScale.Identity, Models.GainEnergyScale.Identity
    ];
}