namespace PSP.PBP.AT3.Models;

public static class CodecInfo
{
    public const int MaxBfus = 32;

    public const int NumSamples = 1024;

    public const int SampleRateHz = 44100;

    public const int MdctSize = 512;

    public const int ExponentOffset = 4;

    public const int LocScale = 3;

    public const int LocSz = 1 << LocScale;

    public const int GainInterpolationPosShift = 15;

    public const int NumSpecs = NumSamples;

    public const int NumQmf = 4;

    public const int MaxSpecs = NumSamples;

    public const int MaxSpecsPerBlock = 128;

    public static readonly float[] ScaleTable = BuildScaleTable();

    public static readonly float[] EncodeWindow = BuildEncodeWindow();

    public static readonly float[] DecodeWindow = BuildDecodeWindow();

    public static readonly float[] GainLevel = BuildGainLevel();

    public static readonly float[] GainInterpolation = BuildGainInterpolation();

    public static readonly float[] MaxQuant = [ 0.0f, 1.5f, 2.5f, 3.5f, 4.5f, 7.5f, 15.5f, 31.5f ];

    public static readonly int[] BlockSizeTab = [ 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 288, 320, 352, 384, 416, 448, 480, 512, 576, 640, 704, 768, 896, 1024 ];

    public static readonly int[] ClcLengthTab = [ 0, 4, 3, 3, 4, 4, 5, 6 ];

    public static readonly int[] BlocksPerBand = [ 0, 18, 26, 30, 32 ];

    public static readonly int[] SpecsPerBlock = [ 8, 8, 8, 8, 8, 8, 8, 8, 16, 16, 16, 16, 16, 16, 16, 16, 32, 32, 32, 32, 32, 32, 32, 32, 32, 32, 64, 64, 64, 64, 128, 128, 128 ];

    public static readonly ContainerParams[] ContainerParamsTable = [ new(66150, 192, true), new(93713, 272, true), new(104738, 304, false), new(132300, 384, false), new(146081, 424, false), new(176400, 512, false), new(264600, 768, false), new(352800, 1024, false) ];

    private static readonly HuffEntry[] HuffTable1 = [ new(0x0, 1), new(0x4, 3), new(0x5, 3), new(0xC, 4), new(0xD, 4), new(0x1C, 5), new(0x1D, 5), new(0x1E, 5), new(0x1F, 5) ];
    private static readonly HuffEntry[] HuffTable2 = [ new(0x0, 1), new(0x4, 3), new(0x5, 3), new(0x6, 3), new(0x7, 3) ];
    private static readonly HuffEntry[] HuffTable3 = [ new(0x0, 1), new(0x4, 3), new(0x5, 3), new(0xC, 4), new(0xD, 4), new(0xE, 4), new(0xF, 4) ];
    private static readonly HuffEntry[] HuffTable5 = [ new(0x0, 2), new(0x2, 3), new(0x3, 3), new(0x8, 4), new(0x9, 4), new(0xA, 4), new(0xB, 4), new(0x1C, 5), new(0x1D, 5), new(0x3C, 6), new(0x3D, 6), new(0x3E, 6), new(0x3F, 6), new(0xC, 4), new(0xD, 4) ];
    private static readonly HuffEntry[] HuffTable6 = BuildHuffTable6();
    private static readonly HuffEntry[] HuffTable7 = BuildHuffTable7();

    public static readonly HuffEntry[][] HuffTables = [ HuffTable1, HuffTable2, HuffTable3, HuffTable1, HuffTable5, HuffTable6, HuffTable7 ];

    private static HuffEntry[] BuildHuffTable6()
    {
        var list = new List<HuffEntry> { new(0x0, 3) };

        AddRange(list, 0x2, 6, 4);
        AddRange(list, 0x14, 6, 5);
        AddRange(list, 0x34, 8, 6);
        AddRange(list, 0x78, 8, 7);
        AddRange(list, 0x8, 2, 4);

        return [.. list];
    }

    private static HuffEntry[] BuildHuffTable7()
    {
        var list = new List<HuffEntry> { new(0x0, 3) };

        AddRange(list, 0x8, 10, 5);
        AddRange(list, 0x24, 16, 6);
        AddRange(list, 0x68, 14, 7);
        AddRange(list, 0xEC, 20, 8);
        AddRange(list, 0x2, 2, 4);

        return [.. list];
    }

    private static void AddRange(List<HuffEntry> list, int start, int count, byte bits)
    {
        for (int i = 0; i < count; i++)
            list.Add(new HuffEntry((byte)(start + i), bits));
    }

    private static float[] BuildScaleTable()
    {
        var t = new float[64];

        for (int i = 0; i < 64; i++)
            t[i] = (float)Math.Pow(2.0, i / 3.0 - 21.0);

        return t;
    }

    private static float[] BuildEncodeWindow()
    {
        var w = new float[256];

        for (int i = 0; i < 256; i++)
            w[i] = (float)(Math.Sin(((i + 0.5) / 256.0 - 0.5) * Math.PI) + 1.0);

        return w;
    }

    private static float[] BuildDecodeWindow()
    {
        var enc = BuildEncodeWindow();
        var w = new float[256];

        for (int i = 0; i < 256; i++)
        {
            double a = enc[i];
            double b = enc[255 - i];

            w[i] = (float)(2.0 * a / (a * a + b * b));
        }

        return w;
    }

    private static float[] BuildGainLevel()
    {
        var g = new float[16];

        for (int i = 0; i < 16; i++)
            g[i] = (float)Math.Pow(2.0, ExponentOffset - i);

        return g;
    }

    private static float[] BuildGainInterpolation()
    {
        var g = new float[31];

        for (int i = 0; i < 31; i++)
            g[i] = (float)Math.Pow(2.0, -1.0 / LocSz * (i - 15));

        return g;
    }

    public static uint MantissaToClcIdx(int mantissa)
    {
        ReadOnlySpan<uint> rtab = [2, 3, 0, 1];

        if (mantissa <= -3 || mantissa >= 2)
            throw new ArgumentOutOfRangeException(nameof(mantissa));

        return rtab[mantissa + 2];
    }

    public static uint MantissasToVlcIndex(int a, int b)
    {
        ReadOnlySpan<uint> rtab = [8, 4, 7, 2, 0, 1, 6, 3, 5];

        if (a <= -2 || a >= 2)
            throw new ArgumentOutOfRangeException(nameof(a));

        if (b <= -2 || b >= 2)
            throw new ArgumentOutOfRangeException(nameof(b));

        return rtab[3 * (a + 1) + (b + 1)];
    }

    public static ContainerParams GetContainerParamsForBitrate(uint bitrate)
    {
        if (bitrate == 0)
            bitrate = 132300;

        foreach (var p in ContainerParamsTable)
        {
            if (p.Bitrate >= bitrate)
                return p;
        }

        throw new ArgumentOutOfRangeException(nameof(bitrate));
    }
}