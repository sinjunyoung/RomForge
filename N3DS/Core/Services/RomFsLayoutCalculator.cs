using N3DS.Core.Models;
using System.Text;

namespace N3DS.Core.Services;

public static class RomFsLayoutCalculator
{
    public static (ulong totalSize, ulong level0Size, ulong offLevel3, ulong offLevel1Hash, ulong offLevel2Hash) CalculateLayout(IReadOnlyList<RomFsDirNode> dirs, IReadOnlyList<RomFsFileNode> files, Dictionary<string, long>? patchSizeMap = null)
    {
        uint dirHashCount = RomFsFormat.GetHashTableCount((uint)dirs.Count);
        uint fileHashCount = RomFsFormat.GetHashTableCount((uint)files.Count);
        uint dirTableLen = 0;

        foreach (var d in dirs)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(d.Entry.Name);

            dirTableLen += d.Entry.Name.Length == 0 ? (uint)RomFsFormat.DirEntryFixed : (uint)(RomFsFormat.DirEntryFixed + RomFsFormat.AlignUp(nameBytes, 4));
        }

        uint fileTableLen = 0;

        foreach (var f in files)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(f.Entry.Name);

            fileTableLen += RomFsFormat.FileEntryFixed + RomFsFormat.AlignUp(nameBytes, 4);
        }

        ulong dataLen = 0;

        foreach (var f in files)
        {
            long size = patchSizeMap?.TryGetValue(f.FullPath, out long ps) == true ? ps : (long)f.DataSize;

            if (size > 0)
                dataLen = RomFsFormat.AlignUp(dataLen, 0x10) + (ulong)size;
        }

        uint romfsHdrSize = RomFsFormat.AlignUp(RomFsFormat.RomFsInfoSize + dirHashCount * 4 + dirTableLen + fileHashCount * 4 + fileTableLen, 0x10);
        ulong level3Size = romfsHdrSize + dataLen;
        ulong level2Size = RomFsFormat.AlignUp(level3Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong level1Size = RomFsFormat.AlignUp(level2Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong masterHashSize = RomFsFormat.AlignUp(level1Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong level0Size = (ulong)RomFsFormat.AlignUp(RomFsFormat.IvfcHeaderSize, RomFsFormat.IvfcHeaderAlign) + masterHashSize;
        ulong offLevel3 = RomFsFormat.AlignUp(level0Size, RomFsFormat.BlockSize);
        ulong offLevel1Hash = RomFsFormat.AlignUp(offLevel3 + level3Size, RomFsFormat.BlockSize);
        ulong offLevel2Hash = RomFsFormat.AlignUp(offLevel1Hash + level1Size, RomFsFormat.BlockSize);
        ulong totalSize = RomFsFormat.AlignUp(offLevel2Hash + level2Size, RomFsFormat.BlockSize);

        return (totalSize, level0Size, offLevel3, offLevel1Hash, offLevel2Hash);
    }
}