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

            if (MatchBytes(header, 0, (byte)'R', (byte)'V', (byte)'Z', 0x01))
            {
                fs.Seek(0, SeekOrigin.Begin);
                var scanBuffer = br.ReadBytes(256);

                for (int i = 0; i <= scanBuffer.Length - 4; i++)
                {
                    if (scanBuffer[i] == 0x5D && scanBuffer[i + 1] == 0x1C && scanBuffer[i + 2] == 0x9E && scanBuffer[i + 3] == 0xA3)
                        return Result(RomFormat.Rvz, ConvertDirection.Decompress, "iso");

                    if (scanBuffer[i] == 0xC2 && scanBuffer[i + 1] == 0x33 && scanBuffer[i + 2] == 0x9F && scanBuffer[i + 3] == 0x3D)
                        return Result(RomFormat.Rvz, ConvertDirection.Decompress, "iso");
                }

                return Result(RomFormat.Rvz, ConvertDirection.Decompress, "iso");
            }

            if (MatchBytes(header, 0, 0x01, 0xC0, 0x0B, 0xB1))
                return Result(RomFormat.Gcz, ConvertDirection.Compress, "rvz");

            if (MatchBytes(header, 0, (byte)'W', (byte)'I', (byte)'A', 0x01))
                return Result(RomFormat.Wia, ConvertDirection.Compress, "rvz");

            if (MatchMagic(header, "WBFS"))
                return Result(RomFormat.Wbfs, ConvertDirection.Compress, "rvz");

            if (fs.Length > 0x104)
            {
                fs.Seek(0x100, SeekOrigin.Begin);
                var magic = br.ReadBytes(4);
                if (MatchMagic(magic, "NCSD"))
                    return Result(RomFormat.Cci, ConvertDirection.Compress, "zcci");
            }

            fs.Seek(0x18, SeekOrigin.Begin);
            var wiiMagic = br.ReadBytes(4);
            if (MatchBytes(wiiMagic, 0, 0x5D, 0x1C, 0x9E, 0xA3))
                return Result(RomFormat.Wii, ConvertDirection.Compress, "rvz");

            fs.Seek(0x1C, SeekOrigin.Begin);
            var gcMagic = br.ReadBytes(4);
            if (MatchBytes(gcMagic, 0, 0xC2, 0x33, 0x9F, 0x3D))
                return Result(RomFormat.Gcm, ConvertDirection.Compress, "rvz");

            fs.Seek(0x8001, SeekOrigin.Begin);
            var cdMagic = br.ReadBytes(5);
            if (MatchMagic(cdMagic, "CD001"))
                return Result(RomFormat.Iso, ConvertDirection.Compress, "chd");
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
                CHD.Core.Models.Enums.ChdSourceType.GdRom => "gdi",
                CHD.Core.Models.Enums.ChdSourceType.BinCue => "cue",
                CHD.Core.Models.Enums.ChdSourceType.ISO => "iso",
                CHD.Core.Models.Enums.ChdSourceType.DVD => "iso",
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

    private static bool MatchBytes(byte[] data, int offset, params byte[] expected)
    {
        if (data.Length < offset + expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (data[offset + i] != expected[i]) return false;
        return true;
    }
}