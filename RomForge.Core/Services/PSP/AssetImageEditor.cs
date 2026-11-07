using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Services.PSP;

public static class AssetImageEditor
{
    public static (byte[] Bytes, BitmapImage? Image) Resize(byte[] rawBytes, int targetWidth, int targetHeight)
    {
        using var input = new MemoryStream(rawBytes);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        var transformed = new TransformedBitmap(source, new ScaleTransform((double)targetWidth / source.PixelWidth, (double)targetHeight / source.PixelHeight));
        var encoder = new PngBitmapEncoder();

        encoder.Frames.Add(BitmapFrame.Create(transformed));

        using var output = new MemoryStream();

        encoder.Save(output);

        byte[] finalBytes = output.ToArray();

        return (finalBytes, finalBytes.ToBitmapImage());
    }
}