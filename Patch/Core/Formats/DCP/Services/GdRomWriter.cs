namespace Patch.Core.Formats.DCP.Services;

public static class GdRomWriter
{
    public static void WriteDataTrack(string outputPath, uint trackStartLba, IReadOnlyCollection<(uint Lba, byte[] Sector2048)> sectors, Action<double, string>? onProgress = null, CancellationToken ct = default)
    {
        if (sectors.Count == 0)
            return;

        onProgress?.Invoke(0.0, "데이터 트랙 섹터 정렬 중...");

        var sortedSectors = sectors.OrderBy(s => s.Lba).ToList();
        uint maxLba = sortedSectors[^1].Lba;
        int totalRequiredSectors = (int)(maxLba - trackStartLba + 1);
        string tempPath = outputPath + ".tmp";

        try
        {
            onProgress?.Invoke(0.0, "데이터 트랙 원시 섹터 생성 및 디스크 쓰기 중...");

            int sectorSize = 2352;
            int writeBufferSize = 2000;
            byte[] writeBuffer = new byte[sectorSize * writeBufferSize];
            int bufferedCount = 0;

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 256 * 1024))
            {
                int sortedIdx = 0;
                var emptyUserData = new byte[2048];

                for (int i = 0; i < totalRequiredSectors; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    uint currentLba = trackStartLba + (uint)i;
                    byte[] userData;

                    if (sortedIdx < sortedSectors.Count && sortedSectors[sortedIdx].Lba == currentLba)
                    {
                        userData = sortedSectors[sortedIdx].Sector2048;
                        sortedIdx++;
                    }
                    else
                        userData = emptyUserData;

                    byte[] rawSector = BuildRawSector(currentLba, userData);

                    Buffer.BlockCopy(rawSector, 0, writeBuffer, bufferedCount * sectorSize, sectorSize);

                    bufferedCount++;

                    if (bufferedCount >= writeBufferSize)
                    {
                        fs.Write(writeBuffer, 0, bufferedCount * sectorSize);

                        bufferedCount = 0;
                    }

                    if (i % 500 == 0)
                        onProgress?.Invoke((double)i / totalRequiredSectors, "데이터 트랙 원시 섹터 생성 및 쓰기 중...");
                }

                if (bufferedCount > 0)
                    fs.Write(writeBuffer, 0, bufferedCount * sectorSize);
            }

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempPath, outputPath);
            onProgress?.Invoke(1.0, "데이터 트랙 작성 완료.");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try 
                {
                    File.Delete(tempPath); 
                }
                catch { }
            }
        }
    }

    public static byte[] BuildRawSector(uint absoluteLba, byte[] userData2048)
    {
        if (userData2048.Length != 2048)
            throw new ArgumentException("유저데이터는 2048바이트여야 합니다.", nameof(userData2048));

        var sector = new byte[2352];

        sector[0] = 0x00;

        for (int i = 1; i <= 10; i++)
            sector[i] = 0xFF;

        sector[11] = 0x00;

        uint msf = absoluteLba + 150;

        sector[12] = ToBcd((byte)(msf / 75 / 60));
        sector[13] = ToBcd((byte)(msf / 75 % 60));
        sector[14] = ToBcd((byte)(msf % 75));
        sector[15] = 0x01;

        Buffer.BlockCopy(userData2048, 0, sector, 16, 2048);

        uint edc = ComputeEdc(sector, 0, 2064);

        sector[2064] = (byte)(edc & 0xFF);
        sector[2065] = (byte)((edc >> 8) & 0xFF);
        sector[2066] = (byte)((edc >> 16) & 0xFF);
        sector[2067] = (byte)((edc >> 24) & 0xFF);

        return sector;
    }

    private static byte ToBcd(byte value) => (byte)(((value / 10) << 4) | (value % 10));

    private static readonly uint[] EdcTable = BuildEdcTable();

    private static uint[] BuildEdcTable()
    {
        var table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint edc = i;

            for (int j = 0; j < 8; j++)
                edc = (edc >> 1) ^ ((edc & 1) != 0 ? 0xD8018001 : 0);

            table[i] = edc;
        }

        return table;
    }

    private static uint ComputeEdc(byte[] data, int offset, int length)
    {
        uint edc = 0;

        for (int i = 0; i < length; i++)
            edc = EdcTable[(edc ^ data[offset + i]) & 0xFF] ^ (edc >> 8);

        return edc;
    }
}