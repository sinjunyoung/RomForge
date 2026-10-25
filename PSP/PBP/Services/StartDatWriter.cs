namespace PSP.PBP.Services;

public static class StartDatWriter
{
    private const uint PBPMAGIC = 0x50425000;

    public static void WriteStartDat(Stream outputStream, byte[] basePbpBytes, byte[]? bootPng)
    {
        using var basePbp = new MemoryStream(basePbpBytes);
        var baseHeader = new uint[10];

        basePbp.Read(baseHeader, 10);

        if (baseHeader[0] != PBPMAGIC)
            throw new Exception("BASE.PBP is not a valid PBP file.");

        var buffer = new byte[1 * 1048576];

        basePbp.Seek(baseHeader[9] + 12, SeekOrigin.Begin);

        var temp = new byte[sizeof(uint)];

        basePbp.Read(temp, 0, 4);

        var x = BitConverter.ToUInt32(temp, 0);

        x += 0x50000;

        basePbp.Seek(x, SeekOrigin.Begin);
        basePbp.Read(buffer, 0, 8);

        var tempstr = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);

        if (tempstr != "STARTDAT")
            throw new Exception("Cannot find STARTDAT in BASE.PBP. Not a valid PSX eboot.pbp");

        var header = new uint[2];

        basePbp.Seek(x + 16, SeekOrigin.Begin);
        basePbp.Read(header, 2);
        basePbp.Seek(x, SeekOrigin.Begin);
        basePbp.Read(buffer, 0, (int)header[0]);

        if (bootPng != null)
        {
            var sizeBytes = BitConverter.GetBytes((uint)bootPng.Length);

            for (var j = 0; j < sizeof(uint); j++)
                buffer[16 + 4 + j] = sizeBytes[j];
        }

        outputStream.Write(buffer, 0, (int)header[0]);

        if (bootPng == null)
        {
            basePbp.Read(buffer, 0, (int)header[1]);
            outputStream.Write(buffer, 0, (int)header[1]);
        }
        else
        {
            outputStream.WriteResource(bootPng);
            basePbp.Read(buffer, 0, (int)header[1]);
        }

        int bytesRead;

        while ((bytesRead = basePbp.Read(buffer, 0, 1048576)) > 0)
            outputStream.Write(buffer, 0, bytesRead);
    }
}