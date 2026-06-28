using System.IO;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Services.PS;

public static class ImageConversion
{
    public static byte[] ToPng(byte[] anyImageBytes)
    {
        using var input = new MemoryStream(anyImageBytes);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(decoder.Frames[0]);

        using var output = new MemoryStream();
        encoder.Save(output);

        return output.ToArray();
    }
}