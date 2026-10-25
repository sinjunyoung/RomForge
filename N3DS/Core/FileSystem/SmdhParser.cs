using N3DS.Core.Enums;
using N3DS.Core.Models;
using System.Text;

namespace N3DS.Core.FileSystem;

public class SmdhParser
{
    private const uint SmdhMagic = 0x48444D53;
    private const int TitleStructSize = 0x200;
    private const int IconLargeOffset = 0x24C0;

    private static readonly LocaleLanguage[] SlotLanguages =
    [
        LocaleLanguage.JP, LocaleLanguage.EN, LocaleLanguage.FR, LocaleLanguage.DE, LocaleLanguage.IT, LocaleLanguage.ES, LocaleLanguage.ZH, LocaleLanguage.KO, LocaleLanguage.NL, LocaleLanguage.PT, LocaleLanguage.RU, LocaleLanguage.TW,
    ];

    public static SmdhInfo? TryParse(byte[] data)
    {
        if (data.Length < 0x36C0) 
            return null;

        uint magic = BitConverter.ToUInt32(data, 0);

        if (magic != SmdhMagic) 
            return null;

        var titles = new SmdhTitle[16];

        for (int i = 0; i < 16; i++)
        {
            int offset = 0x08 + i * TitleStructSize;

            titles[i] = ParseTitle(data, offset);
        }

        SmdhTitle title = titles[1].ShortDesc.Length > 0 ? titles[1] : titles[0].ShortDesc.Length > 0 ? titles[0] : titles.FirstOrDefault(t => t.ShortDesc.Length > 0);
        byte[]? iconPixels = TryDecodeIconToBytes(data, IconLargeOffset, 48, 48);
        var availableLanguages = new List<LocaleLanguage>();

        for (int i = 0; i < SlotLanguages.Length; i++)
        {
            if (titles[i].ShortDesc.Length > 0)
                availableLanguages.Add(SlotLanguages[i]);
        }

        return new SmdhInfo
        {
            ShortDescription = title.ShortDesc,
            LongDescription = title.LongDesc,
            Publisher = title.Publisher,
            IconPixels = iconPixels,
            IconWidth = 48,
            IconHeight = 48,
            AvailableLanguages = availableLanguages,
        };
    }

    private static SmdhTitle ParseTitle(byte[] data, int offset)
    {
        return new SmdhTitle
        {
            ShortDesc = ReadUtf16(data, offset + 0x000, 0x80),
            LongDesc = ReadUtf16(data, offset + 0x080, 0x100),
            Publisher = ReadUtf16(data, offset + 0x180, 0x80),
        };
    }

    private static string ReadUtf16(byte[] data, int offset, int maxBytes)
    {
        int end = offset;
        int limit = Math.Min(offset + maxBytes, data.Length - 1);

        while (end < limit - 1 && (data[end] != 0 || data[end + 1] != 0))
            end += 2;

        return Encoding.Unicode.GetString(data, offset, end - offset);
    }

    private static byte[]? TryDecodeIconToBytes(byte[] data, int offset, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        int srcIndex = offset;

        for (int tileY = 0; tileY < height; tileY += 8)
        {
            for (int tileX = 0; tileX < width; tileX += 8)
            {
                for (int k = 0; k < 64; k++)
                {
                    int x = 0;
                    int y = 0;

                    for (int bit = 0; bit < 3; bit++)
                    {
                        x |= ((k >> (bit * 2)) & 1) << bit;
                        y |= ((k >> (bit * 2 + 1)) & 1) << bit;
                    }

                    ushort rgb565 = BitConverter.ToUInt16(data, srcIndex);

                    srcIndex += 2;

                    byte r = (byte)(((rgb565 >> 11) & 0x1F) * 255 / 31);
                    byte g = (byte)(((rgb565 >> 5) & 0x3F) * 255 / 63);
                    byte b = (byte)((rgb565 & 0x1F) * 255 / 31);
                    int dst = ((tileY + y) * width + (tileX + x)) * 4;

                    pixels[dst + 0] = b;
                    pixels[dst + 1] = g;
                    pixels[dst + 2] = r;
                    pixels[dst + 3] = 255;
                }
            }
        }

        return pixels;
    }
}