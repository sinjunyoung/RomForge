namespace PBP.AT3.Services;

public sealed class AnalysisFilterBank
{
    public const int FrameSamples = 1024;

    public const int SubbandSamples = 256;

    private readonly Qmf _qmf1 = new(FrameSamples);
    private readonly Qmf _qmf2 = new(FrameSamples / 2);
    private readonly Qmf _qmf3 = new(FrameSamples / 2);
    private readonly float[] _buf1 = new float[FrameSamples];
    private readonly float[] _buf2 = new float[FrameSamples];

    public void Analysis(ReadOnlySpan<float> pcm, Span<float> sub0, Span<float> sub1, Span<float> sub2, Span<float> sub3)
    {
        _qmf1.Analysis(pcm, _buf1, _buf2);
        _qmf2.Analysis(_buf1, sub0, sub1);
        _qmf3.Analysis(_buf2, sub3, sub2);
    }
}