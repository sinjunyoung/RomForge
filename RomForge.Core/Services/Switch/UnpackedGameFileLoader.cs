using NSW.Core;
using RomForge.Core.ViewModels.Switch;
using System.Globalization;
using System.IO;
using System.Windows.Media;

namespace RomForge.Core.Services.Switch;

public static class UnpackedGameFileLoader
{
    private const string DlcFolderName = "DLCs";

    public static List<GameFile> Load(string unpackedDir)
    {
        var result = new List<GameFile>();

        if (string.IsNullOrWhiteSpace(unpackedDir) || !Directory.Exists(unpackedDir))
            return result;

        var programDirs = Directory.GetDirectories(unpackedDir)
            .Select(d => (Dir: d, Name: Path.GetFileName(d)))
            .Where(x => x.Name.Length == 16 && ulong.TryParse(x.Name, NumberStyles.HexNumber, null, out _))
            .Select(x => (x.Dir, x.Name, Id: ulong.Parse(x.Name, NumberStyles.HexNumber)))
            .OrderBy(x => x.Id)
            .ToList();

        if (programDirs.Count == 0)
            return result;

        var (Dir, Name, Id) = programDirs[0];
        string controlDir = Path.Combine(Dir, "control");
        string titleName = Name;
        string? version = null;

        try
        {
            var (krTitle, _, displayVersion, _) = LibHacHelper.ReadNacpInfo(Path.Combine(controlDir, "control.nacp"));

            if (!string.IsNullOrWhiteSpace(krTitle))
                titleName = krTitle;

            if (!string.IsNullOrWhiteSpace(displayVersion))
                version = displayVersion;
        }
        catch { }

        var icon = LoadIcon(controlDir);

        result.Add(new GameFile(Dir)
        {
            IsUnpacked = true,
            FileType = "B",
            TitleID = Name,
            TitleName = titleName,
            Version = version,
            Icon = icon,
            CustomSizeBytes = programDirs.Sum(p => GetDirectorySize(p.Dir)),
            ProgramIdOffsets = [.. programDirs.Select(p => (byte)(p.Id - Id))]
        });

        string dlcRoot = Path.Combine(unpackedDir, DlcFolderName);

        if (!Directory.Exists(dlcRoot))
            return result;

        foreach (string dlcDir in Directory.GetDirectories(dlcRoot).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
        {
            string dlcId = Path.GetFileName(dlcDir);

            if (!ulong.TryParse(dlcId, NumberStyles.HexNumber, null, out _))
                continue;

            result.Add(new GameFile(dlcDir)
            {
                IsUnpacked = true,
                FileType = "D",
                TitleID = dlcId,
                TitleName = dlcId.Length >= 4 ? $"{titleName} (DLC {dlcId[^4..]})" : titleName,
                Icon = icon,
                CustomSizeBytes = GetDirectorySize(dlcDir)
            });
        }

        return result;
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadIcon(string controlDir)
    {
        if (!Directory.Exists(controlDir))
            return null;

        var icons = Directory.GetFiles(controlDir, "icon_*.dat");
        string? picked = icons.FirstOrDefault(p => Path.GetFileName(p).Contains("Korean", StringComparison.OrdinalIgnoreCase)) ?? icons.FirstOrDefault(p => Path.GetFileName(p).Contains("AmericanEnglish", StringComparison.OrdinalIgnoreCase)) ?? icons.FirstOrDefault();

        if (picked == null)
            return null;

        try
        {
            return File.ReadAllBytes(picked).ToBitmapImage();
        }
        catch
        {
            return null;
        }
    }

    private static long GetDirectorySize(string dir)
    {
        try
        {
            return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}