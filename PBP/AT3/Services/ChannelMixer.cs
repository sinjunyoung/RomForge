namespace PBP.AT3.Services;

public static class ChannelMixer
{
    private const float Half = 0.5f;
    private const float Minus3Db = 0.70710678f;

    private static uint DefaultMask(int channels) => channels switch
    {
        3 => 0x007u,
        4 => 0x033u,
        5 => 0x037u,
        6 => 0x03Fu,
        7 => 0x13Fu,
        8 => 0x63Fu,
        _ => 0u
    };

    private static (float L, float R) Gains(int position) => position switch
    {
        0 => (1.0f, 0.0f),
        1 => (0.0f, 1.0f),
        2 => (Minus3Db, Minus3Db),
        3 => (0.0f, 0.0f),
        4 => (Minus3Db, 0.0f),
        5 => (0.0f, Minus3Db),
        6 => (Minus3Db, 0.0f),
        7 => (0.0f, Minus3Db),
        8 => (Half, Half),
        9 => (Minus3Db, 0.0f),
        10 => (0.0f, Minus3Db),
        11 => (Half, Half),
        12 => (Half, 0.0f),
        13 => (Half * Minus3Db, Half * Minus3Db),
        14 => (0.0f, Half),
        15 => (Half, 0.0f),
        16 => (Half * Minus3Db, Half * Minus3Db),
        17 => (0.0f, Half),
        _ => (0.0f, 0.0f)
    };

    public static float[] ToStereo(float[] samples, int channels, uint channelMask)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(channels, 2);

        if (channelMask == 0)
            channelMask = DefaultMask(channels);

        var gainL = new float[channels];
        var gainR = new float[channels];
        int ch = 0;

        for (int bit = 0; bit < 32 && ch < channels; bit++)
        {
            if ((channelMask & (1u << bit)) == 0)
                continue;

            var (L, R) = Gains(bit);

            gainL[ch] = L;
            gainR[ch] = R;
            ch++;
        }

        if (ch == 0)
        {
            gainL[0] = 1.0f;
            gainR[1 % channels] = 1.0f;
        }

        long frames = samples.Length / channels;
        var output = new float[frames * 2];
        float peak = 0.0f;

        for (long i = 0; i < frames; i++)
        {
            float l = 0.0f;
            float r = 0.0f;

            for (int c = 0; c < channels; c++)
            {
                float v = samples[i * channels + c];

                l += v * gainL[c];
                r += v * gainR[c];
            }

            output[i * 2] = l;
            output[i * 2 + 1] = r;

            peak = MathF.Max(peak, MathF.Max(MathF.Abs(l), MathF.Abs(r)));
        }

        if (peak > 1.0f)
        {
            float scale = 1.0f / peak;

            for (long i = 0; i < output.Length; i++)
                output[i] *= scale;
        }

        return output;
    }
}