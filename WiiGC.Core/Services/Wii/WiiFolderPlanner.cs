using System.Buffers.Binary;
using System.Text;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiFolderPlanner
{
    private const int FileAlignment = 0x20;
    private const int MaximumHeaderSize = 0x1000000;

    public static WiiRepackPlan Plan(string folder, IReadOnlyDictionary<string, string>? overlay)
    {
        string sys = Path.Combine(folder, WiiFolderLayout.Sys);
        string filesRoot = Path.Combine(folder, WiiFolderLayout.Files);
        string bootPath = Path.Combine(sys, WiiFolderLayout.Boot);
        string bi2Path = Path.Combine(sys, WiiFolderLayout.Bi2);
        string apploaderPath = Path.Combine(sys, WiiFolderLayout.Apploader);
        string dolPath = Path.Combine(sys, WiiFolderLayout.Dol);

        if (!Directory.Exists(filesRoot) || !File.Exists(bootPath) || !File.Exists(bi2Path) || !File.Exists(apploaderPath) || !File.Exists(dolPath))
            throw new InvalidDataException("언팩 폴더가 올바르지 않습니다 (sys, files 폴더가 없음). 이 버전에서 다시 언팩해 주세요.");

        byte[] boot = File.ReadAllBytes(bootPath);
        byte[] bi2 = File.ReadAllBytes(bi2Path);
        byte[] apploader = File.ReadAllBytes(apploaderPath);
        long dolSize = new FileInfo(dolPath).Length;

        if (boot.Length != WiiFolderLayout.BootSize)
            throw new InvalidDataException("boot.bin 크기가 올바르지 않습니다 (0x440바이트여야 합니다).");

        if (bi2.Length != WiiFolderLayout.Bi2Size)
            throw new InvalidDataException("bi2.bin 크기가 올바르지 않습니다 (0x2000바이트여야 합니다).");

        if (apploader.Length < 0x20)
            throw new InvalidDataException("apploader.img 크기가 올바르지 않습니다.");

        if (dolSize < WiiDol.HeaderSize)
            throw new InvalidDataException("main.dol 크기가 올바르지 않습니다.");

        long dolOffset = ReadOffset(boot, 0x420);
        long fstMaxSize = ReadOffset(boot, 0x42C);
        long apploaderEnd = WiiFolderLayout.ApploaderOffset + apploader.Length;

        if (dolOffset < apploaderEnd || dolOffset > MaximumHeaderSize)
            throw new InvalidDataException("apploader가 DOL 위치와 겹치거나 boot.bin의 DOL 위치가 올바르지 않습니다.");

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

            if (resolved.Count > 0 && resolved.All(r => !r.Found))
                throw new InvalidDataException($"패치 파일이 이 디스크에 하나도 없습니다 (총 {resolved.Count}개). 다른 게임이거나 다른 버전용 패치일 수 있습니다: {string.Join(", ", resolved.Take(3).Select(r => r.Path))}");
        }

        if (files.Count == 0)
            throw new InvalidDataException("files 폴더에 파일이 없습니다.");

        var tree = new WiiFstTree();
        var sources = new Dictionary<WiiFstNode, string>();
        var nodes = new Dictionary<string, WiiFstNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (discPath, filePath) in files)
        {
            var parent = tree.Root;
            string[] parts = discPath.TrimStart('/').Split('/');

            for (int i = 0; i < parts.Length - 1; i++)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(parts[i]);
                var directory = parent.Children.FirstOrDefault(c => c.IsDirectory && c.NameBytes.AsSpan().SequenceEqual(nameBytes));

                if (directory == null)
                {
                    directory = new WiiFstNode(nameBytes, true);

                    parent.Children.Add(directory);
                }

                parent = directory;
            }

            var file = new WiiFstNode(Encoding.UTF8.GetBytes(parts[^1]), false);

            parent.Children.Add(file);
            sources[file] = filePath;
            nodes[discPath] = file;
        }

        SortChildren(tree.Root);

        var ordered = new List<WiiFstNode>();

        Collect(tree.Root, ordered);

        long newFstSize = tree.GetSerializedSize();
        long newFstOffset = Align(dolOffset + dolSize);
        long cursor = Align(newFstOffset + newFstSize);
        var extents = new List<WiiRepackedPartition.Extent>();

        foreach (var node in ordered)
        {
            string path = sources[node];
            long size = new FileInfo(path).Length;

            if (size > uint.MaxValue)
                throw new InvalidDataException($"파일이 너무 큽니다 (4GB 미만이어야 합니다): {path}");

            if (size > 0)
                extents.Add(WiiRepackedPartition.Extent.FromFile(cursor, size, path));

            node.Offset = cursor;
            node.Size = size;
            cursor = Align(cursor + size);
        }

        byte[] head = new byte[dolOffset];

        boot.CopyTo(head, 0);
        bi2.CopyTo(head, WiiFolderLayout.BootSize);
        apploader.CopyTo(head, WiiFolderLayout.ApploaderOffset);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x424), (uint)(newFstOffset >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x428), (uint)(newFstSize >> 2));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(0x42C), (uint)(Math.Max(fstMaxSize, newFstSize) >> 2));
        extents.Add(WiiRepackedPartition.Extent.FromMemory(0, head));
        extents.Add(WiiRepackedPartition.Extent.FromFile(dolOffset, dolSize, dolPath));
        extents.Add(WiiRepackedPartition.Extent.FromMemory(newFstOffset, tree.Serialize()));

        long clusters = (cursor + WiiLayout.BlockDataSize - 1) / WiiLayout.BlockDataSize;
        var entries = resolved.Select(r => new WiiPatchEntry(r.Path, r.Found, r.Found ? nodes[r.Path].Offset : 0)).ToList();

        return new WiiRepackPlan(new WiiRepackedPartition(extents, clusters * WiiLayout.BlockDataSize), clusters * WiiLayout.BlockTotalSize, entries);
    }

    private static Dictionary<string, string> CollectFiles(string filesRoot)
    {
        string root = Path.GetFullPath(filesRoot);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            result["/" + Path.GetRelativePath(root, file).Replace('\\', '/')] = file;

        return result;
    }

    private static void SortChildren(WiiFstNode directory)
    {
        directory.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var child in directory.Children.Where(c => c.IsDirectory))
            SortChildren(child);
    }

    private static void Collect(WiiFstNode directory, List<WiiFstNode> files)
    {
        foreach (var child in directory.Children)
        {
            if (child.IsDirectory)
                Collect(child, files);
            else
                files.Add(child);
        }
    }

    private static long ReadOffset(byte[] boot, int position) => (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(position)) << 2;

    private static long Align(long value) => (value + FileAlignment - 1) / FileAlignment * FileAlignment;
}