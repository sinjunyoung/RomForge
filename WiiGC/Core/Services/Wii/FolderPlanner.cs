using Common.Localization;

using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal static class FolderPlanner
{
    public static RepackPlan Plan(string folder, IReadOnlyDictionary<string, string>? overlay)
    {
        string sys = Path.Combine(folder, FolderLayout.Sys);
        string filesRoot = Path.Combine(folder, FolderLayout.Files);
        string bootPath = Path.Combine(sys, FolderLayout.Boot);
        string bi2Path = Path.Combine(sys, FolderLayout.Bi2);
        string apploaderPath = Path.Combine(sys, FolderLayout.Apploader);
        string dolPath = Path.Combine(sys, FolderLayout.Dol);

        if (!Directory.Exists(filesRoot) || !File.Exists(bootPath) || !File.Exists(bi2Path) || !File.Exists(apploaderPath) || !File.Exists(dolPath))
            throw new InvalidDataException(Loc.S.ErrUnpackFolderInvalid);

        byte[] boot = File.ReadAllBytes(bootPath);
        byte[] bi2 = File.ReadAllBytes(bi2Path);
        byte[] apploader = File.ReadAllBytes(apploaderPath);
        long dolSize = new FileInfo(dolPath).Length;

        if (boot.Length != FolderLayout.BootSize)
            throw new InvalidDataException(Loc.S.ErrBootBinSize);

        if (bi2.Length != FolderLayout.Bi2Size)
            throw new InvalidDataException(Loc.S.ErrBi2BinSize);

        if (apploader.Length < 0x20)
            throw new InvalidDataException(Loc.S.ErrApploaderSizeInvalid);

        if (dolSize < Dol.HeaderSize)
            throw new InvalidDataException(Loc.S.ErrMainDolSizeInvalid);

        long dolOffset = LayoutBuilder.ReadOffset(boot, 0x420);
        long fstMaxSize = LayoutBuilder.ReadOffset(boot, 0x42C);
        long apploaderEnd = FolderLayout.ApploaderOffset + apploader.Length;

        if (dolOffset < apploaderEnd || dolOffset > LayoutBuilder.MaximumHeaderSize)
            throw new InvalidDataException(Loc.S.ErrApploaderOverlapsDol);

        var files = CollectFiles(filesRoot);
        var resolved = new List<(string Path, bool Found)>();

        if (overlay != null)
        {
            foreach (var (discPath, filePath) in overlay)
            {
                bool found = files.ContainsKey(discPath);

                if (found)
                    files[discPath] = filePath;

                resolved.Add((discPath, found));
            }

            LayoutBuilder.EnsureAnyApplied(resolved);
        }

        if (files.Count == 0)
            throw new InvalidDataException(Loc.S.ErrFilesFolderEmpty);

        var tree = new WiiFstTree();
        var sources = new Dictionary<FstNode, string>();
        var nodes = new Dictionary<string, FstNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (discPath, filePath) in files)
        {
            var parent = tree.Root;
            string[] parts = discPath.TrimStart('/').Split('/');

            for (int i = 0; i < parts.Length - 1; i++)
            {
                byte[] nameBytes = FstName.Encode(parts[i]);
                var directory = parent.Children.FirstOrDefault(c => c.IsDirectory && c.NameBytes.AsSpan().SequenceEqual(nameBytes));

                if (directory == null)
                {
                    directory = new FstNode(nameBytes, true);

                    parent.Children.Add(directory);
                }

                parent = directory;
            }

            var file = new FstNode(FstName.Encode(parts[^1]), false);

            parent.Children.Add(file);

            sources[file] = filePath;
            nodes[discPath] = file;
        }

        SortChildren(tree.Root);

        var ordered = new List<FstNode>();

        Collect(tree.Root, ordered);

        var layout = new LayoutBuilder(tree, dolOffset, dolSize);

        foreach (var node in ordered)
        {
            string path = sources[node];
            long size = new FileInfo(path).Length;

            LayoutBuilder.EnsureFileSize(size, path);
            layout.PlaceFile(node, size, path);
        }

        byte[] head = new byte[dolOffset];

        boot.CopyTo(head, 0);
        bi2.CopyTo(head, FolderLayout.BootSize);
        apploader.CopyTo(head, FolderLayout.ApploaderOffset);

        var entries = resolved.Select(r => new PatchEntry(r.Path, r.Found, r.Found ? nodes[r.Path].Offset : 0)).ToList();

        return layout.Build(head, fstMaxSize, RepackedPartition.Extent.FromFile(dolOffset, dolSize, dolPath), entries);
    }

    private static Dictionary<string, string> CollectFiles(string filesRoot)
    {
        string root = Path.GetFullPath(filesRoot);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            result["/" + Path.GetRelativePath(root, file).Replace('\\', '/')] = file;

        return result;
    }

    private static void SortChildren(FstNode directory)
    {
        directory.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var child in directory.Children.Where(c => c.IsDirectory))
            SortChildren(child);
    }

    private static void Collect(FstNode directory, List<FstNode> files)
    {
        foreach (var child in directory.Children)
        {
            if (child.IsDirectory)
                Collect(child, files);
            else
                files.Add(child);
        }
    }
}