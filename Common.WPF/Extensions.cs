using SixLabors.ImageSharp.PixelFormats;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace Common.WPF;

public static class Extensions
{
    public static BitmapImage? ToBitmapImage(this byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return null;

        var image = new BitmapImage();
        using var memoryStream = new MemoryStream(imageData);

        image.BeginInit();

        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memoryStream;

        image.EndInit();
        image.Freeze();

        return image;
    }

    public static byte[] ResizePng(this byte[] imageBytes, int width, int height)
    {
        using var input = new MemoryStream(imageBytes);
        using var img = Image.FromStream(input);
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);

        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        g.DrawImage(img, 0, 0, width, height);

        using var output = new MemoryStream();

        bmp.Save(output, ImageFormat.Png);

        return output.ToArray();
    }

    public static byte[] ToImageBytes(this string filePath)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(filePath);
        byte[] pixels = new byte[image.Width * image.Height * 4];

        image.CopyPixelDataTo(pixels);

        using var tempBitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        var bitmapData = tempBitmap.LockBits(new Rectangle(0, 0, tempBitmap.Width, tempBitmap.Height), ImageLockMode.WriteOnly, tempBitmap.PixelFormat);

        Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
        tempBitmap.UnlockBits(bitmapData);

        using var cleanBitmap = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(cleanBitmap))
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.DrawImage(tempBitmap, 0, 0, 256, 256);
        }

        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

        if (encoder != null)
        {
            var encoderParameters = new EncoderParameters(1);

            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

            cleanBitmap.Save(ms, encoder, encoderParameters);
        }
        else
            cleanBitmap.Save(ms, ImageFormat.Jpeg);

        return ms.ToArray();
    }
}