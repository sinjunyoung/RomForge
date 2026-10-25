namespace PSP.PBP.AT3.Services;

internal static class AdpcmDecoder
{
    private static readonly int[] ImaStepTable =
    [
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97,
        107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
        876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428,
        4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350,
        22385, 24623, 27086, 29794, 32767
    ];

    private static readonly int[] ImaIndexTable = [ -1, -1, -1, -1, 2, 4, 6, 8 ];

    private static readonly int[] MsAdaptTable = [ 230, 230, 230, 230, 307, 409, 512, 614, 768, 614, 512, 409, 307, 230, 230, 230 ];

    private static readonly short[] MsDefaultCoefs = [ 256, 0, 512, -256, 0, 0, 192, 64, 240, 0, 460, -208, 392, -232 ];

    private static short Clip16(int v) => (short)Math.Clamp(v, short.MinValue, short.MaxValue);

    public static float[] DecodeIma(int channels, int blockAlign, byte[] data)
    {
        if (channels < 1 || channels > 2 || blockAlign < 4 * channels)
            throw new NotSupportedException("Unsupported IMA ADPCM layout");

        int blocks = (data.Length + blockAlign - 1) / blockAlign;
        int samplesPerBlock = 1 + (blockAlign - 4 * channels) * 2 / channels;
        var output = new float[(long)blocks * samplesPerBlock * channels];
        long outPos = 0;
        var pred = new int[channels];
        var index = new int[channels];

        for (int blk = 0; blk < blocks; blk++)
        {
            int start = blk * blockAlign;
            int len = Math.Min(blockAlign, data.Length - start);

            if (len < 4 * channels)
                break;

            for (int ch = 0; ch < channels; ch++)
            {
                pred[ch] = (short)(data[start + ch * 4] | (data[start + ch * 4 + 1] << 8));
                index[ch] = Math.Clamp((int)data[start + ch * 4 + 2], 0, 88);
            }

            int dataBytes = len - 4 * channels;
            int groups = dataBytes / (4 * channels);
            int framesInBlock = 1 + groups * 8;
            long blockBase = outPos;

            for (int ch = 0; ch < channels; ch++)
                output[blockBase + ch] = pred[ch] / 32768.0f;

            int p = start + 4 * channels;

            for (int g = 0; g < groups; g++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        byte b = data[p++];

                        for (int half = 0; half < 2; half++)
                        {
                            int nib = half == 0 ? (b & 0x0F) : (b >> 4);
                            int step = ImaStepTable[index[ch]];
                            int delta = nib & 7;
                            int diff = ((2 * delta + 1) * step) >> 3;

                            if ((nib & 8) != 0)
                                diff = -diff;

                            pred[ch] = Clip16(pred[ch] + diff);
                            index[ch] = Math.Clamp(index[ch] + ImaIndexTable[delta], 0, 88);

                            int frame = 1 + g * 8 + k * 2 + half;

                            output[blockBase + (long)frame * channels + ch] = pred[ch] / 32768.0f;
                        }
                    }
                }
            }

            outPos += (long)framesInBlock * channels;
        }

        if (outPos != output.Length)
            Array.Resize(ref output, (int)outPos);

        return output;
    }

    public static float[] DecodeMs(int channels, int blockAlign, short[] coefPairs, byte[] data)
    {
        if (channels < 1 || channels > 2 || blockAlign < 7 * channels)
            throw new NotSupportedException("Unsupported MS ADPCM layout");

        short[] coefs = coefPairs.Length >= 14 ? coefPairs : MsDefaultCoefs;
        int numCoef = coefs.Length / 2;
        int blocks = (data.Length + blockAlign - 1) / blockAlign;
        int maxSamplesPerBlock = 2 + (blockAlign - 7 * channels) * 2 / channels;
        var output = new float[(long)blocks * maxSamplesPerBlock * channels];
        long outPos = 0;
        var coef1 = new int[channels];
        var coef2 = new int[channels];
        var delta = new int[channels];
        var s1 = new int[channels];
        var s2 = new int[channels];

        for (int blk = 0; blk < blocks; blk++)
        {
            int start = blk * blockAlign;
            int len = Math.Min(blockAlign, data.Length - start);

            if (len < 7 * channels)
                break;

            int p = start;

            for (int ch = 0; ch < channels; ch++)
            {
                int idx = Math.Min((int)data[p++], numCoef - 1);

                coef1[ch] = coefs[idx * 2];
                coef2[ch] = coefs[idx * 2 + 1];
            }

            for (int ch = 0; ch < channels; ch++)
            {
                delta[ch] = (short)(data[p] | (data[p + 1] << 8));
                p += 2;
            }

            for (int ch = 0; ch < channels; ch++)
            {
                s1[ch] = (short)(data[p] | (data[p + 1] << 8));
                p += 2;
            }

            for (int ch = 0; ch < channels; ch++)
            {
                s2[ch] = (short)(data[p] | (data[p + 1] << 8));
                p += 2;
            }

            for (int ch = 0; ch < channels; ch++)
                output[outPos + ch] = s2[ch] / 32768.0f;

            for (int ch = 0; ch < channels; ch++)
                output[outPos + channels + ch] = s1[ch] / 32768.0f;

            int nibbleBytes = start + len - p;
            int totalNibbles = nibbleBytes * 2;
            int framesFromNibbles = totalNibbles / channels;
            long baseOut = outPos + 2L * channels;
            int nibbleIdx = 0;

            for (int n = 0; n < totalNibbles; n++)
            {
                byte b = data[p + n / 2];
                int nib = (n & 1) == 0 ? (b >> 4) : (b & 0x0F);
                int ch = n % channels;
                int frame = n / channels;
                int predictor = (s1[ch] * coef1[ch] + s2[ch] * coef2[ch]) / 256;

                predictor += ((nib & 8) != 0 ? nib - 16 : nib) * delta[ch];

                int sample = Clip16(predictor);

                s2[ch] = s1[ch];
                s1[ch] = sample;
                delta[ch] = (MsAdaptTable[nib] * delta[ch]) >> 8;

                if (delta[ch] < 16)
                    delta[ch] = 16;

                output[baseOut + (long)frame * channels + ch] = sample / 32768.0f;
                nibbleIdx++;
            }

            outPos += (2L + framesFromNibbles) * channels;
        }

        if (outPos != output.Length)
            Array.Resize(ref output, (int)outPos);

        return output;
    }
}