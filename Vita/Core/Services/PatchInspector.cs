using Patch.Core.Services;

namespace Vita.Core.Services;

public static class PatchInspector
{
    public static (int TotalFiles, int PatchFiles) Inspect(string patchPath)
    {
        using var accessor = SourceAccessorFactory.Open(patchPath);

        int total = 0;
        int patchFiles = 0;

        foreach (string file in accessor.EnumerateAllFiles())
        {
            total++;

            if (PatchFileIndex.PatchExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                patchFiles++;
        }

        return (total, patchFiles);
    }
}