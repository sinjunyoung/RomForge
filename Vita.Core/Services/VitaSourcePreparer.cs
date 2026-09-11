using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaSourcePreparer
{
    private readonly WorkBinReader _workBinReader = new();
    private readonly VitaNoNpDrmDecryptor _decryptor = new();

    public static List<VitaSourceItem> DiscoverItems(string extractedRoot)
    {
        var items = new List<VitaSourceItem>();
        string appDir = Path.Combine(extractedRoot, "app");

        if (Directory.Exists(appDir))
        {
            foreach (var titleDir in Directory.EnumerateDirectories(appDir))
            {
                items.Add(new VitaSourceItem
                {
                    Category = VitaContentCategory.App,
                    TitleId = Path.GetFileName(titleDir),
                    SourcePath = titleDir
                });
            }
        }

        string patchDir = Path.Combine(extractedRoot, "patch");

        if (Directory.Exists(patchDir))
        {
            foreach (var titleDir in Directory.EnumerateDirectories(patchDir))
            {
                items.Add(new VitaSourceItem
                {
                    Category = VitaContentCategory.Patch,
                    TitleId = Path.GetFileName(titleDir),
                    SourcePath = titleDir
                });
            }
        }

        string addcontDir = Path.Combine(extractedRoot, "addcont");

        if (Directory.Exists(addcontDir))
        {
            foreach (var titleDir in Directory.EnumerateDirectories(addcontDir))
            {
                string titleId = Path.GetFileName(titleDir);

                foreach (var contentDir in Directory.EnumerateDirectories(titleDir))
                {
                    items.Add(new VitaSourceItem
                    {
                        Category = VitaContentCategory.Addcont,
                        TitleId = titleId,
                        ContentIdSuffix = Path.GetFileName(contentDir),
                        SourcePath = contentDir
                    });
                }
            }
        }

        return items;
    }

    public VitaPrepareResult PrepareOne(VitaSourceItem item, string outputRoot)
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
            var license = _workBinReader.ReadFromTitlePath(item.SourcePath);

            _decryptor.Decrypt(item.SourcePath, outputPath, license.Klicensee);

            return new VitaPrepareResult { Item = item, OutputPath = outputPath };
        }
        catch (Exception ex)
        {
            return new VitaPrepareResult { Item = item, Error = ex.Message };
        }
    }

    public List<VitaPrepareResult> PrepareAll(string extractedRoot, string outputRoot)
    {
        var items = DiscoverItems(extractedRoot);
        var results = new List<VitaPrepareResult>();

        foreach (var item in items)
            results.Add(PrepareOne(item, outputRoot));

        return results;
    }
}