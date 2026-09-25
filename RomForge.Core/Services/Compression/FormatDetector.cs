using CHD.Core.Models;
using DolphinTool.Core.Models;
using DolphinTool.Core.Rvz;
using RomForge.Core.Models.Compression;
using System.IO;
using System.Text;

namespace RomForge.Core.Services.Compression;

public static class FormatDetector
{
    public static DetectResult Detect(string filePath)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

        switch (ext)
        {
            case "cue": return Result(RomFormat.Cue, ConvertDirection.Compress, "chd");
            case "bin": return Result(RomFormat.Bin, ConvertDirection.Compress, "chd");
            case "gdi": return Result(RomFormat.Gdi, ConvertDirection.Compress, "chd");
            case "ccd": return Result(RomFormat.Ccd, ConvertDirection.Compress, "chd");
            case "img" when File.Exists(Path.ChangeExtension(filePath, ".ccd")):
                return Result(RomFormat.Ccd, ConvertDirection.Compress, "chd");
            case "nsp": return Result(RomFormat.Nsp, ConvertDirection.Compress, "nsz");
            case "nsz": return Result(RomFormat.Nsz, ConvertDirection.Decompress, "nsp");
            case "xci": return Result(RomFormat.Xci, ConvertDirection.Compress, "xcz");
            case "xcz": return Result(RomFormat.Xcz, ConvertDirection.Decompress, "xci");
            case "zcci": return Result(RomFormat.ZCci, ConvertDirection.Decompress, "cci");
            case "cia": return Result(RomFormat.Cia, ConvertDirection.Decompress, "zcci");
        }

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            var header = br.ReadBytes(16);

            if (MatchMagic(header, "Z3DS"))
                return Result(RomFormat.ZCci, ConvertDirection.Decompress, "cci");

            if (MatchMagic(header, "MComprHD"))
            {
                fs.Dispose();
                return DetectChdResult(filePath);
            }

            var container = DiscImageInspector.DetectContainer(filePath);

            if (container == DiscContainerFormat.Rvz)
                return Result(RomFormat.Rvz, ConvertDirection.Decompress, "iso");

            if (container == DiscContainerFormat.Gcz)
                return Result(RomFormat.Gcz, ConvertDirection.Compress, "rvz");

            if (container == DiscContainerFormat.Wia)
                return Result(RomFormat.Wia, ConvertDirection.Compress, "rvz");

            if (container == DiscContainerFormat.Wbfs)
                return Result(RomFormat.Wbfs, ConvertDirection.Compress, "rvz");

            if (fs.Length > 0x104)
            {
                fs.Seek(0x100, SeekOrigin.Begin);
                var magic = br.ReadBytes(4);
                if (MatchMagic(magic, "NCSD"))
                    return Result(RomFormat.Cci, ConvertDirection.Compress, "zcci");
            }

            var platform = DiscImageInspector.Detect(filePath);

            if (platform == DiscPlatform.Wii)
                return Result(RomFormat.Wii, ConvertDirection.Compress, "rvz");

            if (platform == DiscPlatform.GameCube)
                return Result(RomFormat.Gcm, ConvertDirection.Compress, "rvz");

            fs.Seek(0x8001, SeekOrigin.Begin);
            var cdMagic = br.ReadBytes(5);
            if (MatchMagic(cdMagic, "CD001") || MatchMagic(cdMagic, "BEA01"))
                return Result(RomFormat.Iso, ConvertDirection.Compress, "chd");

            if (fs.Length > 0x9320)
            {
                fs.Seek(0x9311, SeekOrigin.Begin);
                var rawMode1Magic = br.ReadBytes(5);
                if (MatchMagic(rawMode1Magic, "CD001"))
                    return Result(RomFormat.Iso, ConvertDirection.Compress, "chd");

                fs.Seek(0x9319, SeekOrigin.Begin);
                var rawMode2Magic = br.ReadBytes(5);
                if (MatchMagic(rawMode2Magic, "CD001"))
                    return Result(RomFormat.Iso, ConvertDirection.Compress, "chd");
            }
        }
        catch { }

        return new DetectResult { Format = RomFormat.Unknown, Direction = ConvertDirection.Unknown };
    }

    private static DetectResult DetectChdResult(string filePath)
    {
        try
        {
            var info = CHD.Core.Services.ChdInfoReader.ReadChdInfo(filePath);

            var outExt = info.SourceType switch
            {
                ChdSourceType.GdRom => "gdi",
                ChdSourceType.BinCue => "cue",
                ChdSourceType.ISO => "cue",
                ChdSourceType.DVD => "iso",
                _ => "iso"
            };

            return Result(RomFormat.Chd, ConvertDirection.Decompress, outExt);
        }
        catch
        {
            return Result(RomFormat.Chd, ConvertDirection.Decompress, "iso");
        }
    }

    private static DetectResult Result(RomFormat format, ConvertDirection dir, string outExt) => new()
    {
        Format = format,
        Direction = dir,
        OutputExtension = outExt,
    };

    private static bool MatchMagic(byte[] data, string magic)
    {
        var bytes = Encoding.ASCII.GetBytes(magic);
        if (data.Length < bytes.Length) return false;
        for (int i = 0; i < bytes.Length; i++)
            if (data[i] != bytes[i]) return false;
        return true;
    }
}