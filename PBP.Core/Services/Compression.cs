using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PBP.Core.Services;

public static class Compression
{
    public static int Compress(byte[] inbuf, byte[] outbuf, int level)
    {
        using var ms = new MemoryStream(outbuf);
        var deflater = new Deflater(level, true);
        using var outStream = new DeflaterOutputStream(ms, deflater);

        outStream.Write(inbuf, 0, inbuf.Length);
        outStream.Flush();
        outStream.Finish();

        return (int)ms.Position;
    }

    public static byte[] Decompress(byte[] input, int outputSize)
    {
        var output = new byte[outputSize];
        using var ms = new MemoryStream(input);
        var inflater = new Inflater(true);
        using var inflaterStream = new InflaterInputStream(ms, inflater);

        int bytesRead = 0;

        while (bytesRead < outputSize)
        {
            int read = inflaterStream.Read(output, bytesRead, outputSize - bytesRead);

            if (read <= 0)
                break;

            bytesRead += read;
        }

        return output;
    }
}