using Darnelix.Webp.Decoding;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace RomForge.Core;

public static class Extensions
{
    public static BitmapImage? ToBitmapImage(this byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0)
            return null;

        using var memoryStream = new MemoryStream(imageData);

        return memoryStream.ToBitmapImage();
    }

    public static BitmapImage? ToBitmapImage(this Stream stream)
    {
        var image = new BitmapImage();

        image.BeginInit();

        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;

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
        using var tempBitmap = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        if (Path.GetExtension(filePath).Equals(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var image = WebpDecoder.Decode(File.ReadAllBytes(filePath));
            byte[] pixels = image.Pixels.ToArray();

            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);

            using var sourceBitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bitmapData = sourceBitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, sourceBitmap.PixelFormat);

            try
            {
                Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            }
            finally
            {
                sourceBitmap.UnlockBits(bitmapData);
            }

            using var graphics = Graphics.FromImage(tempBitmap);

            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            graphics.Clear(Color.Transparent);
            graphics.DrawImage(sourceBitmap, 0, 0, 256, 256);
        }
        else
        {
            using var sourceBitmap = new Bitmap(filePath);
            using var graphics = Graphics.FromImage(tempBitmap);

            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            graphics.Clear(Color.Transparent);
            graphics.DrawImage(sourceBitmap, 0, 0, 256, 256);
        }

        using var ms = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

        if (encoder != null)
        {
            using var encoderParameters = new EncoderParameters(1);

            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

            tempBitmap.Save(ms, encoder, encoderParameters);
        }
        else
            tempBitmap.Save(ms, ImageFormat.Jpeg);

        return ms.ToArray();
    }
}