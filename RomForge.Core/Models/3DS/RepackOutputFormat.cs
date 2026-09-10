namespace RomForge.Core.Models._3DS;

public enum RepackOutputFormat
{
    Cci,
    Zcci,
    Cia,
}

public static class RepackOutputFormatExtensions
{
    public static string ToFileExtension(this RepackOutputFormat format) => format switch
    {
        RepackOutputFormat.Cia => ".cia",
        RepackOutputFormat.Zcci => ".zcci",
        RepackOutputFormat.Cci => ".cci",
        _ => throw new NotSupportedException($"지원하지 않는 포맷: {format}"),
    };
}