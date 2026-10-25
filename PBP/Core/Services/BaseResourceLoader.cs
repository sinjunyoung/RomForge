using System.IO.Compression;

namespace PBP.Core.Services;

public static class BaseResourceLoader
{
    public static byte[] GetBasePbpBytes()
    {
        using var zipStream = new MemoryStream(Properties.Resources.BASE);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries[0];
        using var entryStream = entry.Open();
        using var output = new MemoryStream();

        entryStream.CopyTo(output);

        return output.ToArray();
    }
}