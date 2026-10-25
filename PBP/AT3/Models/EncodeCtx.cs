namespace PBP.AT3.Models;

internal sealed class EncodeCtx
{
    public SingleChannelElement Sce = null!;

    public ushort TargetBits;

    public uint BfuIdxConst;

    public float Loudness;

    public bool AllocInitDone;

    public float Spread;

    public int NumBfu = 1;

    public byte CodingMode = 1;

    public uint[] PrecisionPerBlock = new uint[1];

    public float[] EnergyErr = new float[1];

    public int[] Mantissas = new int[CodecInfo.MaxSpecs];
}