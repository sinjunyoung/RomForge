using Vita.Core.Models;

namespace Vita.Core.Services;

public static class UnpackedScanner
{
    public static List<UnpackedTitle> Scan(string unpackedRoot)
    {
        var titles = new List<UnpackedTitle>();

        if (!Directory.Exists(unpackedRoot))
            return titles;

        string appRoot = Path.Combine(unpackedRoot, "app");

        if (Directory.Exists(appRoot))
        {
            foreach (string titleDir in Directory.EnumerateDirectories(appRoot))
            {
                string titleId = Path.GetFileName(titleDir);
                var (name, icon) = ReadMetadata(titleDir);

                long sizeBytes = GetDirectorySize(titleDir);
                string patchDir = Path.Combine(unpackedRoot, "patch", titleId);
                if (Directory.Exists(patchDir)) sizeBytes += GetDirectorySize(patchDir);

                titles.Add(new UnpackedTitle
                {
                    TitleId = titleId,
                    Category = ContentCategory.App,
                    FolderPath = unpackedRoot,
                    TitleName = name,
                    IconBytes = icon,
                    SizeBytes = sizeBytes
                });
            }
        }

        string patchRoot = Path.Combine(unpackedRoot, "patch");

        if (Directory.Exists(patchRoot))
        {
            foreach (string patchDir in Directory.EnumerateDirectories(patchRoot))
            {
                string titleId = Path.GetFileName(patchDir);
                var (name, icon) = ReadMetadata(patchDir);

                titles.Add(new UnpackedTitle
                {
                    TitleId = titleId,
                    Category = ContentCategory.Patch,
                    FolderPath = unpackedRoot,
                    TitleName = name ?? $"Patch [{titleId}]",
                    IconBytes = icon,
                    SizeBytes = GetDirectorySize(patchDir)
                });
            }
        }

        string addcontRoot = Path.Combine(unpackedRoot, "addcont");

        if (Directory.Exists(addcontRoot))
        {
            foreach (string titleDir in Directory.EnumerateDirectories(addcontRoot))
            {
                string titleId = Path.GetFileName(titleDir);

                foreach (string dlcDir in Directory.EnumerateDirectories(titleDir))
                {
                    string contentId = Path.GetFileName(dlcDir);
                    var (name, icon) = ReadMetadata(dlcDir);

                    titles.Add(new UnpackedTitle
                    {
                        TitleId = titleId,
                        ContentIdSuffix = contentId,
                        Category = ContentCategory.Addcont,
                        FolderPath = unpackedRoot,
                        TitleName = name ?? contentId,
                        IconBytes = icon,
                        SizeBytes = GetDirectorySize(dlcDir)
                    });
                }
            }
        }

        return titles;
    }

    private static (string? Name, byte[]? Icon) ReadMetadata(string targetFolder)
    {
        string sfoPath = Path.Combine(targetFolder, "sce_sys", "param.sfo");
        string iconPath = Path.Combine(targetFolder, "sce_sys", "icon0.png");
        string workBinPath = Path.Combine(targetFolder, "sce_sys", "package", "work.bin");
        string? name = null;
        byte[]? icon = null;

        try
        {
            if (File.Exists(sfoPath))
                name = SfoParser.GetString(SfoParser.Parse(File.ReadAllBytes(sfoPath)), "TITLE");
        }
        catch { }

        try
        {
            if (File.Exists(iconPath))
            {
                if (File.Exists(workBinPath))
                {
                    try
                    {
                        using var accessor = SourceAccessorFactory.Open(targetFolder);
                        var license = WorkBinReader.Read(accessor, "sce_sys/package/work.bin");
                        var table = NoNpDrmDecryptor.ParseFileTable(accessor, string.Empty);

                        for (int i = 0; i < table.Entries.Count; i++)
                        {
                            var entry = table.Entries[i];

                            if (entry.Type.IsDirectory())
                                continue;

                            string relativePath = (entry.RelativePath ?? entry.Name).Replace('\\', '/').TrimStart('/');

                            if (!relativePath.Equals("sce_sys/icon0.png", StringComparison.OrdinalIgnoreCase))
                                continue;

                            icon = NoNpDrmDecryptor.DecryptEntry(accessor, string.Empty, license.Klicensee, entry, table.UnicvEntries[i], table.FilesSalt);
                            break;
                        }
                    }
                    catch { }
                }

                if (icon == null)
                {
                    byte[] raw = File.ReadAllBytes(iconPath);

                    if (raw.Length > 8 && raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47)
                        icon = raw;
                }
            }
        }
        catch { }

        return (name, icon);
    }

    private static long GetDirectorySize(string dirPath)
    {
        try
        {
            if (!Directory.Exists(dirPath))
                return 0;

            return Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }
}