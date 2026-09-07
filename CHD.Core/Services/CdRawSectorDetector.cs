using CHD.Core.Models;

namespace CHD.Core.Services;

public static class CdRawSectorDetector
{
    private const int SectorSize = 2352;
    private const long Sector16Offset = 16L * SectorSize;
    private const int Mode1HeaderSize = 16;
    private const int Mode2HeaderSize = 24;

    public static CdRawSectorMode Detect(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (fs.Length <= Sector16Offset + Mode2HeaderSize + 5)
                return CdRawSectorMode.None;

            using var br = new BinaryReader(fs);

            fs.Seek(Sector16Offset + Mode1HeaderSize + 1, SeekOrigin.Begin);

            if (IsCd001(br.ReadBytes(5)))
                return CdRawSectorMode.Mode1Raw;

            fs.Seek(Sector16Offset + Mode2HeaderSize + 1, SeekOrigin.Begin);

            if (IsCd001(br.ReadBytes(5)))
                return CdRawSectorMode.Mode2Raw;
        }
        catch
        {
        }

        return CdRawSectorMode.None;
    }

    private static bool IsCd001(byte[] data) =>
        data.Length == 5 && data[0] == 'C' && data[1] == 'D' && data[2] == '0' && data[3] == '0' && data[4] == '1';
}