using SixLabors.ImageSharp.PixelFormats;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace RomForge.Core.Services.Switch;

public static class SaturnCoverArtFetcher
{
    private const string BaseUrl = "https://raw.githubusercontent.com/sinjunyoung/ss-covers/main/covers/default";

    private static readonly HttpClient Http = new();

    public static async Task<byte[]?> TryDownloadCoverPngAsync(string gameId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gameId)) 
            return null;

        try 
        {
            byte[] imageBytes = await Http.GetByteArrayAsync($"{BaseUrl}/{gameId}.jpg", ct);
            using var msInput = new MemoryStream(imageBytes);
            using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(msInput);
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

            using var msOutput = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

            if (encoder != null)
            {
                var encoderParameters = new EncoderParameters(1);

                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                cleanBitmap.Save(msOutput, encoder, encoderParameters);
            }
            else
                cleanBitmap.Save(msOutput, ImageFormat.Jpeg);

            return msOutput.ToArray();
        }
        catch 
        { 
            return null; 
        }
    }
}