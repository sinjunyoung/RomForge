using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core;
using NSW.M1.Core.Models;
using Path = System.IO.Path;
using Res = NSW.Core.Properties.Resources;

namespace NSW.M1.Core.Services;

public static class UnpackedDirScanner
{
    public static UnpackResult Scan(string unpackedDir, uint? overrideSdkVersion = null, byte? overrideKeyGeneration = null)
    {
        var programDirs = Directory.GetDirectories(unpackedDir)
            .Select(d => new { Dir = d, Name = Path.GetFileName(d) })
            .Where(x => x.Name.Length == 16 && ulong.TryParse(x.Name, System.Globalization.NumberStyles.HexNumber, null, out _))
            .Select(x => new { x.Dir, Id = ulong.Parse(x.Name, System.Globalization.NumberStyles.HexNumber) })
            .OrderBy(x => x.Id)
            .ToList();

        if (programDirs.Count == 0)
            throw new Exception("언팩된 프로그램 폴더(titleId)를 찾을 수 없습니다.");

        ulong baseTitleId = programDirs[0].Id;

        string nacpPath = Path.Combine(programDirs[0].Dir, "control", "control.nacp");
        string controlFile = Directory.GetFiles(unpackedDir, "control*.nca").FirstOrDefault() ?? string.Empty;
        var (krTitle, enTitle, displayVersion, titleId) = LibHacHelper.ReadNacpInfo(nacpPath);

        byte keyGeneration = 1;
        uint sdkVersion = 0;
        uint gameVersion = 0;

        if (File.Exists(controlFile))
        {
            (keyGeneration, sdkVersion) = LibHacHelper.ReadControlNcaInfo(controlFile);

            string fileName = Path.GetFileNameWithoutExtension(controlFile);

            if (fileName.Contains('_'))
                _ = uint.TryParse(fileName.Split('_')[1], out gameVersion);
        }

        var dlcs = new List<DlcUnpackInfo>();
        string dlcBaseDir = Path.Combine(unpackedDir, "DLCs");

        if (Directory.Exists(dlcBaseDir))
        {
            foreach (var dlcDir in Directory.GetDirectories(dlcBaseDir))
            {
                string titleIdStr = Path.GetFileName(dlcDir);
                if (ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong dlcTitleId))
                {
                    dlcs.Add(new DlcUnpackInfo
                    {
                        TitleId = dlcTitleId,
                        Dir = Path.Combine("DLCs", titleIdStr)
                    });
                }
            }
        }

        var exefsDirs = new Dictionary<byte, string>();
        var romfsDirs = new Dictionary<byte, string>();
        var logoDirs = new Dictionary<byte, string>();
        var controlDirs = new Dictionary<byte, string>();
        var htmlDocDirs = new Dictionary<byte, string>();
        var legalDirs = new Dictionary<byte, string>();
        var rawProgramNcaPaths = new Dictionary<byte, string>();

        foreach (var pd in programDirs)
        {
            byte idOffset = (byte)(pd.Id - baseTitleId);

            string exefs = Path.Combine(pd.Dir, "exefs");
            if (Directory.Exists(exefs)) exefsDirs[idOffset] = exefs;

            string romfs = Path.Combine(pd.Dir, "romfs");
            if (Directory.Exists(romfs)) romfsDirs[idOffset] = romfs;

            string logo = Path.Combine(pd.Dir, "logo");
            if (Directory.Exists(logo)) logoDirs[idOffset] = logo;

            string control = Path.Combine(pd.Dir, "control");
            if (Directory.Exists(control)) controlDirs[idOffset] = control;

            string htmldoc = Path.Combine(pd.Dir, "htmldoc");
            if (Directory.Exists(htmldoc)) htmlDocDirs[idOffset] = htmldoc;

            string legal = Path.Combine(pd.Dir, "legal");
            if (Directory.Exists(legal)) legalDirs[idOffset] = legal;
        }

        string rawDir = Path.Combine(unpackedDir, "rawprograms");

        if (Directory.Exists(rawDir))
        {
            var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);

            foreach (var ncaFile in Directory.GetFiles(rawDir, "*.nca"))
            {
                using var fs = new FileStream(ncaFile, FileMode.Open, FileAccess.Read);
                var nca = new Nca(keySet, new StreamStorage(fs, false));

                byte offset = (byte)(nca.Header.TitleId - baseTitleId);
                rawProgramNcaPaths[offset] = ncaFile;
            }
        }

        return new UnpackResult
        {
            TitleId = titleId,
            GameVersion = gameVersion,
            BaseSdkVersion = overrideSdkVersion ?? sdkVersion,
            BaseKeyGeneration = overrideKeyGeneration ?? keyGeneration,
            DisplayVersion = displayVersion,
            KrTitle = krTitle,
            EnTitle = enTitle,
            ExefsDirs = exefsDirs,
            RomfsDirs = romfsDirs,
            LogoDirs = logoDirs,
            ControlDirs = controlDirs,
            HtmlDocDirs = htmlDocDirs,
            LegalDirs = legalDirs,
            Dlcs = dlcs,
            RawProgramNcaPaths = rawProgramNcaPaths,
        };
    }
}