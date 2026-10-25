using N3DS.Core.Enums;
using N3DS.Core.FileSystem;
using N3DS.Core.Models;
using RomForge.Core.Models._3DS;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RomForge.Core.Services._3DS;

public static class RomInfoParser
{
    public static async Task<TitleViewModel?> ParseFromFileAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            TitleParseResult? result = await Util.ParseFile(path);

            var vm = new TitleViewModel
            {
                FilePath = path,
                Title = result.Title!,
                ProductCode = result.ProductCode!,
                ShortDescription = result.ShortDescription!,
                Publisher = result.Publisher!,
                Crypto = result.Crypto,
                AvailableLanguages = result.AvailableLanguages
            };

            if (result.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, result.IconPixels, 48 * 4);
                bitmap.Freeze();
                vm.Icon = bitmap;
            }

            vm.CommitOriginalValues();

            return vm;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<TitleViewModel?> ParseFromUnpackedAsync(string outputPath)
    {
        try
        {
            string partition0 = Path.Combine(outputPath, "unpacked", "partition0");
            string headerPath = Path.Combine(partition0, "header.bin");
            string iconPath = Path.Combine(partition0, "exefs", "icon.bin");

            if (!File.Exists(headerPath))
                return null;

            byte[] headerRaw = await File.ReadAllBytesAsync(headerPath);
            var ncchHeader = NcchHeader.Parse(headerRaw);

            SmdhInfo? smdhInfo = null;

            if (File.Exists(iconPath))
            {
                byte[] iconData = await File.ReadAllBytesAsync(iconPath);

                smdhInfo = SmdhParser.TryParse(iconData);
            }

            var vm = new TitleViewModel
            {
                FilePath = string.Empty,
                Title = new InstalledTitle
                {
                    TitleId = ncchHeader.ProgramId.ToString("x16"),
                    Version = ncchHeader.Version,
                    ContentSize = 0,
                    ContentPath = string.Empty,
                    Type = (TitleType)(ncchHeader.ProgramId >> 32)
                },
                ProductCode = ncchHeader.ProductCodeString,
                ShortDescription = smdhInfo?.ShortDescription ?? string.Empty,
                Publisher = smdhInfo?.Publisher ?? string.Empty,
                Crypto = !ncchHeader.NoCrypto,
                AvailableLanguages = smdhInfo?.AvailableLanguages ?? []
            };

            if (smdhInfo?.IconPixels is not null)
            {
                var bitmap = BitmapSource.Create(48, 48, 96, 96, PixelFormats.Bgr32, null, smdhInfo.IconPixels, 48 * 4);

                bitmap.Freeze();

                vm.Icon = bitmap;
            }

            vm.CommitOriginalValues();

            return vm;
        }
        catch
        {
            return null;
        }
    }
}