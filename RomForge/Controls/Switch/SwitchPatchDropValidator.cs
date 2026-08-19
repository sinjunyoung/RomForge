using System.IO;
using System.Windows;

namespace RomForge.Controls.Switch;

public static class SwitchPatchDropValidator
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

    public static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        var opts = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System | FileAttributes.Hidden };

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*.*", opts))
                    yield return f;
            else if (File.Exists(path))
                yield return path;
        }
    }
}