using PSP.PBP.AT3.Enums;

namespace PSP.PBP.AT3.Models;

public class ConverterOptions
{
    public Bitrate BitrateKbps { get; set; } = Bitrate.Kbps132;

    public int MaxSeconds { get; set; } = 30;

    public bool EnableTonal { get; set; } = true;

    public bool EnableGain { get; set; } = true;

    public Action<double>? OnProgress { get; set; }
}