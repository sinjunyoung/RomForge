using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiPartitionPlanner
{
    private const int BootSize = 0x440;
    private const int MinimumHeaderSize = 0x2440;
    private const int MaximumFstSize = 0x4000000;

    public static WiiRepackPlan Plan(WiiPartitionReader original, IReadOnlyDictionary<string, string> replacements)
    {
        byte[] boot = new byte[BootSize];

        original.Read(0, boot);

        long dolOffset = WiiLayoutBuilder.ReadOffset(boot, 0x420);
        long fstOffset = WiiLayoutBuilder.ReadOffset(boot, 0x424);
        long fstSize = WiiLayoutBuilder.ReadOffset(boot, 0x428);
        long fstMaxSize = WiiLayoutBuilder.ReadOffset(boot, 0x42C);

        if (dolOffset < MinimumHeaderSize || dolOffset > WiiLayoutBuilder.MaximumHeaderSize || fstSize < 12 || fstSize > MaximumFstSize || fstOffset + fstSize > original.Length)
            throw new InvalidDataException("Wii 파티션 부트 정보가 올바르지 않습니다.");

        byte[] dolHeader = new byte[WiiDol.HeaderSize];

        original.Read(dolOffset, dolHeader);

        long dolSize = WiiDol.GetSize(dolHeader);

        if (dolOffset + dolSize > original.Length)
            throw new InvalidDataException("DOL 크기가 올바르지 않습니다.");

        byte[] fstBytes = new byte[fstSize];

        original.Read(fstOffset, fstBytes);

        var tree = WiiFstTree.Parse(fstBytes);
        var files = tree.EnumerateFiles().ToList();
        var byPath = new Dictionary<string, WiiFstNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, node) in files)
            byPath.TryAdd(path, node);

        var external = new Dictionary<WiiFstNode, (string Path, long Size)>();
        var resolved = new List<(string Path, WiiFstNode? Node)>(replacements.Count);

        foreach (var (discPath, filePath) in replacements)
        {
            bool found = byPath.TryGetValue(discPath, out var node);

            if (found)
            {
                long length = new FileInfo(filePath).Length;

                WiiLayoutBuilder.EnsureFileSize(length, filePath);

                external[node!] = (filePath, length);
            }

            resolved.Add((discPath, found ? node : null));
        }

        WiiLayoutBuilder.EnsureAnyApplied(resolved.Select(r => (r.Path, r.Node != null)).ToList());

        byte[] head = new byte[dolOffset];

        original.Read(0, head);

        var ordered = files.Select(f => f.Node).OrderBy(n => n.Offset).ToList();
        var oldPlacement = ordered.Select(n => (n.Offset, n.Size)).ToList();
        var layout = new WiiLayoutBuilder(tree, dolOffset, dolSize);

        for (int i = 0; i < ordered.Count; i++)
        {
            var node = ordered[i];
            var (oldOffset, oldSize) = oldPlacement[i];

            if (external.TryGetValue(node, out var replacement))
                layout.PlaceFile(node, replacement.Size, replacement.Path);
            else
                layout.PlaceFile(node, oldSize, original, oldOffset);
        }

        var entries = resolved.Select(r => new WiiPatchEntry(r.Path, r.Node != null, r.Node?.Offset ?? 0)).ToList();

        return layout.Build(head, fstMaxSize, WiiRepackedPartition.Extent.FromReader(dolOffset, dolSize, original, dolOffset), entries);
    }
}