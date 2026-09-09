using System.Text;
using System.Text.RegularExpressions;

namespace PBP.Core.Services;

public static class Iso9660GameIdExtractor
{
    public enum ExtractResult { Success, NotPs1Disc, InvalidDisc }

    public static (string? Id, ExtractResult Result) Extract(Func<uint, byte[]> sectorReader)
    {
        var pvd = sectorReader(16);

        if (pvd[0] != 0x01 || pvd[1] != 'C' || pvd[2] != 'D' || pvd[3] != '0' || pvd[4] != '0' || pvd[5] != '1')
            return (null, ExtractResult.InvalidDisc);

        var rootDirLba = BitConverter.ToUInt32(pvd, 156 + 2);
        var id = FindSystemCnf(sectorReader, rootDirLba);

        id ??= ScanForGameId(sectorReader);

        return id != null ? (id, ExtractResult.Success) : (null, ExtractResult.NotPs1Disc);
    }

    private static string? FindSystemCnf(Func<uint, byte[]> sectorReader, uint dirLba)
    {
        var sector = sectorReader(dirLba);
        var pos = 0;

        while (pos < sector.Length)
        {
            var recordLen = sector[pos];

            if (recordLen == 0)
                break;

            var nameLen = sector[pos + 32];

            if (pos + 33 + nameLen <= sector.Length)
            {
                var name = Encoding.ASCII.GetString(sector, pos + 33, nameLen).Split(';')[0].Trim().ToUpperInvariant();

                if (name == "SYSTEM.CNF")
                {
                    var fileLba = BitConverter.ToUInt32(sector, pos + 2);
                    return ReadSystemCnf(sectorReader, fileLba);
                }
            }

            pos += recordLen;
        }

        return null;
    }

    private static string? ReadSystemCnf(Func<uint, byte[]> sectorReader, uint lba)
    {
        var content = Encoding.ASCII.GetString(sectorReader(lba));

        var id = Regex.Match(content, @"([A-Z]{4})_(\d{3})\.(\d+)", RegexOptions.IgnoreCase);

        if (!id.Success)
            return null;

        return $"{id.Groups[1].Value.ToUpperInvariant()}{id.Groups[2].Value}{id.Groups[3].Value}";
    }

    private static string? ScanForGameId(Func<uint, byte[]> sectorReader)
    {
        for (uint i = 0; i < 32; i++)
        {
            try
            {
                var sector = sectorReader(i);
                var content = Encoding.ASCII.GetString(sector);
                var matchHyphen = Regex.Match(content, @"(S[A-Z]{3})-(\d{5})", RegexOptions.IgnoreCase);

                if (matchHyphen.Success)
                    return $"{matchHyphen.Groups[1].Value.ToUpperInvariant()}{matchHyphen.Groups[2].Value}";

                var matchDot = Regex.Match(content, @"(S[A-Z]{3})_(\d{3})\.(\d+)", RegexOptions.IgnoreCase);

                if (matchDot.Success)
                    return $"{matchDot.Groups[1].Value.ToUpperInvariant()}{matchDot.Groups[2].Value}{matchDot.Groups[3].Value}";
            }
            catch
            {
            }
        }

        return null;
    }
}