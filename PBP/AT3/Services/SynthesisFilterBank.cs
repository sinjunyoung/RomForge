namespace PBP.AT3.Services;

public sealed class SynthesisFilterBank
{
    public const int FrameSamples = 1024;

    public const int SubbandSamples = 256;

    private readonly Qmf _qmf1 = new(FrameSamples);
    private readonly Qmf _qmf2 = new(FrameSamples / 2);
    private readonly Qmf _qmf3 = new(FrameSamples / 2);
    private readonly float[] _buf1 = new float[FrameSamples / 2];
    private readonly float[] _buf2 = new float[FrameSamples / 2];

    public void Synthesis(ReadOnlySpan<float> sub0, ReadOnlySpan<float> sub1, ReadOnlySpan<float> sub2, ReadOnlySpan<float> sub3, Span<float> pcm)
    {
        _qmf2.Synthesis(_buf1, sub0, sub1);
        _qmf3.Synthesis(_buf2, sub3, sub2);
        _qmf1.Synthesis(pcm, _buf1, _buf2);
    }
}