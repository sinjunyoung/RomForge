using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaSourcePreparer
{
    public static List<VitaSourceItem> DiscoverItems(string extractedRoot)
    {
        using var accessor = new FolderSourceAccessor(extractedRoot);

        return DiscoverItems(accessor);
    }

    public static List<VitaSourceItem> DiscoverItems(IVitaSourceAccessor accessor)
    {
        var items = new List<VitaSourceItem>();

        if (accessor.DirectoryExists("app"))
        {
            foreach (var titleId in accessor.EnumerateDirectoryNames("app"))
            {
                items.Add(new VitaSourceItem
                {
                    Category = VitaContentCategory.App,
                    TitleId = titleId,
                    SourcePath = $"app/{titleId}"
                });
            }
        }

        if (accessor.DirectoryExists("patch"))
        {
            foreach (var titleId in accessor.EnumerateDirectoryNames("patch"))
            {
                items.Add(new VitaSourceItem
                {
                    Category = VitaContentCategory.Patch,
                    TitleId = titleId,
                    SourcePath = $"patch/{titleId}"
                });
            }
        }

        if (accessor.DirectoryExists("addcont"))
        {
            foreach (var titleId in accessor.EnumerateDirectoryNames("addcont"))
            {
                foreach (var contentId in accessor.EnumerateDirectoryNames($"addcont/{titleId}"))
                {
                    items.Add(new VitaSourceItem
                    {
                        Category = VitaContentCategory.Addcont,
                        TitleId = titleId,
                        ContentIdSuffix = contentId,
                        SourcePath = $"addcont/{titleId}/{contentId}"
                    });
                }
            }
        }

        return items;
    }

    public static VitaPrepareResult PrepareOne(VitaSourceItem item, string outputRoot)
    {
        string categoryFolder = item.Category switch
        {
            VitaContentCategory.App => "app",
            VitaContentCategory.Patch => "patch",
            VitaContentCategory.Addcont => "addcont",
            _ => throw new NotSupportedException()
        };

        string outputPath = item.Category == VitaContentCategory.Addcont ? Path.Combine(outputRoot, categoryFolder, item.TitleId, item.ContentIdSuffix ?? "") : Path.Combine(outputRoot, categoryFolder, item.TitleId);

        try
        {
            string workBinPath = Path.Combine(item.SourcePath, "sce_sys", "package", "work.bin");
            var license = WorkBinReader.Read(workBinPath);

            VitaNoNpDrmDecryptor.Decrypt(item.SourcePath, outputPath, license.Klicensee);
            VitaLicenseInstaller.Install(license, workBinPath, outputRoot);

            return new VitaPrepareResult { Item = item, OutputPath = outputPath };
        }
        catch (Exception ex)
        {
            return new VitaPrepareResult { Item = item, Error = ex.Message };
        }
    }

    public static List<VitaPrepareResult> PrepareAll(string extractedRoot, string outputRoot)
    {
        var items = DiscoverItems(extractedRoot);
        var results = new List<VitaPrepareResult>();

        foreach (var item in items)
            results.Add(PrepareOne(item, outputRoot));

        return results;
    }
}