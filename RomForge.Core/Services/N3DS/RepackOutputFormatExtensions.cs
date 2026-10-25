using RomForge.Core.Models.N3DS;

namespace RomForge.Core.Services.N3DS;

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