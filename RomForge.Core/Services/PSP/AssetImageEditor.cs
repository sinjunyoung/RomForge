using Common.WPF;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Services.PSP;

public static class AssetImageEditor
{
    public static (byte[] Bytes, BitmapImage Image) Resize(byte[] rawBytes, int targetWidth, int targetHeight)
    {
        using var image = Image.Load<Bgra32>(rawBytes);

        image.Mutate(x => x.Resize(targetWidth, targetHeight));

        using var ms = new MemoryStream();

        image.SaveAsPng(ms, new PngEncoder
        {
            CompressionLevel = PngCompressionLevel.BestCompression
        });

        var finalBytes = ms.ToArray();

        ms.Position = 0;

        BitmapImage bitmapImage = ms.ToBitmapImage()!;

        return (finalBytes, bitmapImage);
    }
}