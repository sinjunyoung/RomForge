using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal static class PartitionPlanner
{
    private const int MinimumHeaderSize = 0x2440;
    private const int MaximumFstSize = 0x4000000;

    public static RepackPlan Plan(PartitionReader original, IReadOnlyDictionary<string, string> replacements, bool requireAnyApplied = true)
    {
        byte[] boot = new byte[FolderLayout.BootSize];

        original.Read(0, boot);

        long dolOffset = LayoutBuilder.ReadOffset(boot, 0x420);
        long fstOffset = LayoutBuilder.ReadOffset(boot, 0x424);
        long fstSize = LayoutBuilder.ReadOffset(boot, 0x428);
        long fstMaxSize = LayoutBuilder.ReadOffset(boot, 0x42C);

        if (dolOffset < MinimumHeaderSize || dolOffset > LayoutBuilder.MaximumHeaderSize || fstSize < 12 || fstSize > MaximumFstSize || fstOffset + fstSize > original.Length)
            throw new InvalidDataException("Wii 파티션 부트 정보가 올바르지 않습니다.");

        byte[] dolHeader = new byte[Dol.HeaderSize];

        original.Read(dolOffset, dolHeader);

        long dolSize = Dol.GetSize(dolHeader);

        if (dolOffset + dolSize > original.Length)
            throw new InvalidDataException("DOL 크기가 올바르지 않습니다.");

        byte[] fstBytes = new byte[fstSize];

        original.Read(fstOffset, fstBytes);

        var tree = WiiFstTree.Parse(fstBytes);
        var files = tree.EnumerateFiles().ToList();
        var byPath = new Dictionary<string, FstNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, node) in files)
            byPath.TryAdd(path, node);

        var external = new Dictionary<FstNode, (string Path, long Size)>();
        var resolved = new List<(string Path, FstNode? Node)>(replacements.Count);

        foreach (var (discPath, filePath) in replacements)
        {
            bool found = byPath.TryGetValue(discPath, out var node);

            if (found)
            {
                long length = new FileInfo(filePath).Length;

                LayoutBuilder.EnsureFileSize(length, filePath);

                external[node!] = (filePath, length);
            }

            resolved.Add((discPath, found ? node : null));
        }

        if (requireAnyApplied)
            LayoutBuilder.EnsureAnyApplied(resolved.Select(r => (r.Path, r.Node != null)).ToList());

        byte[] head = new byte[dolOffset];

        original.Read(0, head);

        var ordered = files.Select(f => f.Node).OrderBy(n => n.Offset).ToList();
        var oldPlacement = ordered.Select(n => (n.Offset, n.Size)).ToList();
        var layout = new LayoutBuilder(tree, dolOffset, dolSize);

        for (int i = 0; i < ordered.Count; i++)
        {
            var node = ordered[i];
            var (oldOffset, oldSize) = oldPlacement[i];

            if (external.TryGetValue(node, out var replacement))
                layout.PlaceFile(node, replacement.Size, replacement.Path);
            else
                layout.PlaceFile(node, oldSize, original, oldOffset);
        }

        var entries = resolved.Select(r => new PatchEntry(r.Path, r.Node != null, r.Node?.Offset ?? 0)).ToList();

        return layout.Build(head, fstMaxSize, RepackedPartition.Extent.FromReader(dolOffset, dolSize, original, dolOffset), entries);
    }
}