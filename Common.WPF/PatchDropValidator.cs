using System.IO;
using System.Windows;

namespace Common.WPF;

public static class PatchDropValidator
{
    public static bool IsValidPatchDrop(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length != 1)
            return false;

        return IsValidPatchPath(paths[0]);
    }

    public static bool IsValidPatchPath(string path)
    {
        if (Directory.Exists(path))
            return true;

        if (!File.Exists(path))
            return false;

        string ext = Path.GetExtension(path);

        return string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".7z", StringComparison.OrdinalIgnoreCase);
    }
}