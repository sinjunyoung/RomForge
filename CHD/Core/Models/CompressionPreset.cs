namespace CHD.Core.Models;

public class CompressionPreset
{
    public string Name { get; set; }

    public int Version { get; set; }

    public string[] Codecs { get; set; }

    public string Description { get; set; }

    public static readonly CompressionPreset[] Presets =
    [
        new CompressionPreset
        {
            Name = "CD-ROM (v5, LZMA)",
            Version = 5,
            Codecs = ["cdlz"],
            Description = "Best for CD-ROM images, good compression ratio"
        },
        new CompressionPreset
        {
            Name = "CD-ROM (v5, ZLIB)",
            Version = 5,
            Codecs = ["cdzl"],
            Description = "Faster compression for CD-ROM images"
        },
        new CompressionPreset
        {
            Name = "DVD/HDD (v5, LZMA)",
            Version = 5,
            Codecs = ["lzma"],
            Description = "Best for DVD and hard disk images"
        },
        new CompressionPreset
        {
            Name = "DVD/HDD (v5, ZLIB)",
            Version = 5,
            Codecs = ["zlib"],
            Description = "Faster compression for DVD and hard disk images"
        },
        new CompressionPreset
        {
            Name = "Legacy (v4, ZLIB)",
            Version = 4,
            Codecs = ["zlib"],
            Description = "For compatibility with older software"
        }
    ];
}