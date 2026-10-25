namespace PBP.AT3.Models;

public sealed class PcmData
{
    public int Channels;

    public int SampleRate;

    public float[] Samples = [];

    public long FrameCount => Channels == 0 ? 0 : Samples.Length / Channels;
}