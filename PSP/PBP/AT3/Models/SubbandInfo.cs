namespace PSP.PBP.AT3.Models;

public sealed class SubbandInfo
{
    public const int MaxGainPointsNum = 8;

    public List<GainPoint>[] Info;

    public SubbandInfo() => Info = NewInfo(4);

    private static List<GainPoint>[] NewInfo(int n)
    {
        var info = new List<GainPoint>[n];

        for (int i = 0; i < n; i++)
            info[i] = [];

        return info;
    }

    public void ResizeBands(int n)
    {
        var info = NewInfo(n);

        for (int i = 0; i < Math.Min(n, Info.Length); i++)
            info[i] = Info[i];

        Info = info;
    }

    public int GetQmfNum() => Info.Length;

    public List<GainPoint> GetGainPoints(int band) => Info[band];

    public void Reset()
    {
        foreach (var v in Info)
            v.Clear();
    }
}