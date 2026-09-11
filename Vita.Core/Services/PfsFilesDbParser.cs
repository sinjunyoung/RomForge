using System.Text;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class PfsFilesDbParser
{
    private const int PageSize = 0x400;
    private const int MaxFilesInBlock = 9;
    private const int FileNameSize = 68;

    public static List<PfsFlatEntry> Parse(string filesDbPath)
    {
        using var stream = File.OpenRead(filesDbPath);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadBytes(8);

        if (Encoding.ASCII.GetString(magic) != "SCENGPFS")
            throw new InvalidDataException("files.db magic가 올바르지 않습니다.");

        stream.Seek(PageSize, SeekOrigin.Begin);

        var flat = new List<PfsFlatEntry>();
        long length = stream.Length;

        while (stream.Position < length)
        {
            long blockStart = stream.Position;

            reader.ReadUInt32();
            reader.ReadUInt32();

            uint nFiles = reader.ReadUInt32();

            reader.ReadUInt32();

            if (nFiles > MaxFilesInBlock)
                nFiles = 0;

            var names = new (uint ParentIndex, string Name)[MaxFilesInBlock];

            for (int i = 0; i < nFiles; i++)
            {
                uint parentIndex = reader.ReadUInt32();
                var nameBytes = reader.ReadBytes(FileNameSize);
                int nul = Array.IndexOf(nameBytes, (byte)0);
                string name = Encoding.ASCII.GetString(nameBytes, 0, nul < 0 ? nameBytes.Length : nul);

                names[i] = (parentIndex, name);
            }

            int unusedFileHeaderBytes = (MaxFilesInBlock - (int)nFiles) * (4 + FileNameSize);

            if (unusedFileHeaderBytes > 0)
                reader.ReadBytes(unusedFileHeaderBytes);

            var infos = new (uint Idx, PfsFileType Type, uint Size)[10];

            for (int i = 0; i < 10; i++)
            {
                uint idx = reader.ReadUInt32();
                ushort typeVal = reader.ReadUInt16();

                reader.ReadUInt16();

                uint size = reader.ReadUInt32();

                reader.ReadUInt32();

                infos[i] = (idx, (PfsFileType)typeVal, size);
            }

            reader.ReadBytes(10 * 20);

            for (int i = 0; i < nFiles; i++)
            {
                var (idx, entryType, size) = infos[i];

                if (entryType.IsUnexisting())
                    continue;

                flat.Add(new PfsFlatEntry
                {
                    Index = idx,
                    ParentIndex = names[i].ParentIndex,
                    Name = names[i].Name,
                    Type = entryType,
                    Size = size
                });
            }

            stream.Seek(blockStart + PageSize, SeekOrigin.Begin);
        }

        ResolvePaths(flat);

        return flat;
    }

    private static void ResolvePaths(List<PfsFlatEntry> flat)
    {
        var dirMatrix = new Dictionary<uint, uint>();
        var byIndexDir = new Dictionary<uint, PfsFlatEntry>();
        var byIndexFile = new Dictionary<uint, PfsFlatEntry>();

        foreach (var entry in flat)
        {
            if (entry.Type.IsDirectory())
            {
                dirMatrix[entry.Index] = entry.ParentIndex;
                byIndexDir[entry.Index] = entry;
            }
            else
            {
                byIndexFile[entry.Index] = entry;
            }
        }

        foreach (var entry in flat)
        {
            var dirChain = new List<string>();
            uint parentIndex = entry.Type.IsDirectory() ? dirMatrix[entry.Index] : entry.ParentIndex;

            while (parentIndex != 0)
            {
                if (!dirMatrix.TryGetValue(parentIndex, out uint nextParent))
                    throw new InvalidDataException($"부모 디렉토리 인덱스 {parentIndex}를 찾을 수 없습니다.");

                if (!byIndexDir.TryGetValue(parentIndex, out var dirEntry))
                    throw new InvalidDataException($"디렉토리 인덱스 {parentIndex}를 찾을 수 없습니다.");

                dirChain.Add(dirEntry.Name);
                parentIndex = nextParent;
            }

            dirChain.Reverse();
            dirChain.Add(entry.Name);

            entry.RelativePath = Path.Combine([.. dirChain]);
        }
    }
}