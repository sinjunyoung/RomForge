using PBP.Core.Enums;

namespace PBP.Core.Models;

public class DiskSource
{
    public DiskSourceType Type { get; init; }

    public string FilePath { get; init; } = string.Empty;

    public string? CuePath { get; init; }

    public static DiskSource FromIso(string path) => new() { Type = DiskSourceType.Iso, FilePath = path };

    public static DiskSource FromBinCue(string binPath, string cuePath) => new() { Type = DiskSourceType.Bin, FilePath = binPath, CuePath = cuePath };

    public static DiskSource FromChd(string path) => new() { Type = DiskSourceType.Chd, FilePath = path };
}