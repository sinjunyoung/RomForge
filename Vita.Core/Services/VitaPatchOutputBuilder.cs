namespace Vita.Core.Services;

public static class VitaPatchOutputBuilder
{
    public static void BuildEmuOutput(string decryptedRoot, string emuRoot)
    {
        foreach (string titleId in DiscoverTitleIds(decryptedRoot))
        {
            string appSrc = Path.Combine(decryptedRoot, "app", titleId);
            string patchSrc = Path.Combine(decryptedRoot, "patch", titleId);
            string appDst = Path.Combine(emuRoot, "app", titleId);

            if (Directory.Exists(appSrc))
                CopyDirectory(appSrc, appDst, overwrite: true);

            if (Directory.Exists(patchSrc))
                CopyDirectory(patchSrc, appDst, overwrite: true);

            string addcontSrc = Path.Combine(decryptedRoot, "addcont", titleId);
            if (Directory.Exists(addcontSrc))
                CopyDirectory(addcontSrc, Path.Combine(emuRoot, "addcont", titleId), overwrite: true);
        }

        CopyLicense(decryptedRoot, emuRoot);
    }

    public static void BuildRetailOutput(string decryptedRoot, string retailRoot)
    {
        foreach (string titleId in DiscoverTitleIds(decryptedRoot))
        {
            string appSrc = Path.Combine(decryptedRoot, "app", titleId);
            string patchSrc = Path.Combine(decryptedRoot, "patch", titleId);
            string addcontSrc = Path.Combine(decryptedRoot, "addcont", titleId);

            if (Directory.Exists(appSrc))
                CopyDirectory(appSrc, Path.Combine(retailRoot, "app", titleId), overwrite: true);

            if (Directory.Exists(patchSrc))
                CopyDirectory(patchSrc, Path.Combine(retailRoot, "rePatch", titleId), overwrite: true);

            if (Directory.Exists(addcontSrc))
                CopyDirectory(addcontSrc, Path.Combine(retailRoot, "reAddcont", titleId), overwrite: true);
        }

        CopyLicense(decryptedRoot, retailRoot);
    }

    private static void CopyLicense(string decryptedRoot, string outputRoot)
    {
        string licenseSrc = Path.Combine(decryptedRoot, "license");

        if (Directory.Exists(licenseSrc))
            CopyDirectory(licenseSrc, Path.Combine(outputRoot, "license"), overwrite: true);
    }

    private static HashSet<string> DiscoverTitleIds(string decryptedRoot)
    {
        var titleIds = new HashSet<string>();

        foreach (var category in new[] { "app", "patch", "addcont" })
        {
            string categoryDir = Path.Combine(decryptedRoot, category);

            if (!Directory.Exists(categoryDir))
                continue;

            foreach (var titleDir in Directory.EnumerateDirectories(categoryDir))
                titleIds.Add(Path.GetFileName(titleDir));
        }

        return titleIds;
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
    {
        Directory.CreateDirectory(destDir);

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, dir);

            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string destFile = Path.Combine(destDir, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite);
        }
    }
}