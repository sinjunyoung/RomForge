using N3DS.Core.Models;

namespace N3DS.Core.Services;

public static class RomFsFolderScanner
{
    public static (List<RomFsDirNode> dirs, List<RomFsFileNode> files) ScanFolder(string rootDir)
    {
        var dirs = new List<RomFsDirNode>();
        var files = new List<RomFsFileNode>();

        dirs.Add(new RomFsDirNode
        {
            FullPath = "/",
            Entry = new RomFsDirEntry { Name = string.Empty }
        });

        foreach (var absDir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories).OrderBy(x => x))
        {
            string rel = "/" + Path.GetRelativePath(rootDir, absDir).Replace(Path.DirectorySeparatorChar, '/');
            string name = Path.GetFileName(absDir);

            dirs.Add(new RomFsDirNode
            {
                FullPath = rel,
                Entry = new RomFsDirEntry { Name = name }
            });
        }

        foreach (var absFile in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories).OrderBy(x => x))
        {
            string rel = "/" + Path.GetRelativePath(rootDir, absFile).Replace(Path.DirectorySeparatorChar, '/');
            string name = Path.GetFileName(absFile);
            long size = new FileInfo(absFile).Length;

            files.Add(new RomFsFileNode
            {
                FullPath = rel,
                DataOffset = 0,
                DataSize = (ulong)size,
                Entry = new RomFsFileEntry { Name = name }
            });
        }

        return (dirs, files);
    }

    public static RomFsUnpackResult ScanFolderAsUnpackResult(string rootDir)
    {
        var (dirs, files) = ScanFolder(rootDir);

        return new RomFsUnpackResult
        {
            IvfcHeader = new IvfcHeader
            {
                Magic = 0x43465649,
                TypeId = 0x10000,
                MasterHashSize = 0,
                Levels = new IvfcLevelEntry[3],
                HeaderSize = IvfcHeader.Size,
            },
            RomFsHeader = new RomFsHeader
            {
                HeaderSize = RomFsHeader.Size,
                DirHashBucketOffset = RomFsHeader.Size,
            },
            DataLevel2Offset = 0,
            Directories = dirs,
            Files = files,
        };
    }
}