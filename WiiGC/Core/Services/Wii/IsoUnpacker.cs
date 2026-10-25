using System.Buffers.Binary;
using System.Text;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

public static class IsoUnpacker
{
    private const int CopyChunkSize = 0x100000;
    private const int MaximumCertSize = 0x100000;

    public static int Unpack(string inputPath, string outputFolder, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        var target = IsoRebuilder.FindTargets(input, IsoRebuilder.ReadSpecs(input)).FirstOrDefault() ?? throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");

        if (target.ContainerOffset < FolderLayout.DiscHeadSize)
            throw new InvalidDataException("지원하지 않는 Wii 파티션 배치입니다.");

        using var reader = new PartitionReader(input, target);
        string root = Path.GetFullPath(outputFolder);
        string sysFolder = Path.Combine(root, FolderLayout.Sys);
        string metaFolder = Path.Combine(root, FolderLayout.Meta);
        string filesFolder = Path.Combine(root, FolderLayout.Files);
        byte[] boot = new byte[FolderLayout.BootSize];

        reader.Read(0, boot);

        long dolOffset = LayoutBuilder.ReadOffset(boot, 0x420);

        if (dolOffset < FolderLayout.ApploaderOffset + 0x20 || dolOffset > LayoutBuilder.MaximumHeaderSize || dolOffset + Dol.HeaderSize > reader.Length)
            throw new InvalidDataException("Wii 파티션 부트 정보가 올바르지 않습니다.");

        byte[] bi2 = new byte[FolderLayout.Bi2Size];

        reader.Read(FolderLayout.BootSize, bi2);

        byte[] apploaderHeader = new byte[0x20];

        reader.Read(FolderLayout.ApploaderOffset, apploaderHeader);

        long apploaderSize = 0x20 + (long)BinaryPrimitives.ReadUInt32BigEndian(apploaderHeader.AsSpan(0x14)) + BinaryPrimitives.ReadUInt32BigEndian(apploaderHeader.AsSpan(0x18));

        if (FolderLayout.ApploaderOffset + apploaderSize > dolOffset)
            throw new InvalidDataException("apploader 크기가 올바르지 않습니다.");

        byte[] apploader = new byte[apploaderSize];

        reader.Read(FolderLayout.ApploaderOffset, apploader);

        byte[] dolHeader = new byte[Dol.HeaderSize];

        reader.Read(dolOffset, dolHeader);

        long dolSize = Dol.GetSize(dolHeader);

        if (dolOffset + dolSize > reader.Length)
            throw new InvalidDataException("DOL 크기가 올바르지 않습니다.");

        var partitionHeader = PartitionHeader.Read(input, target.ContainerOffset);
        byte[] partition = new byte[FolderLayout.PartitionHeaderSize];

        input.Read(target.ContainerOffset, partition);

        int certSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2AC));
        long certOffset = target.ContainerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2B0)) << 2);

        if (certSize < 0 || certSize > MaximumCertSize || certOffset + certSize > input.Length)
            throw new InvalidDataException("Wii 파티션 인증서 위치가 올바르지 않습니다.");

        byte[] tmd = new byte[partitionHeader.TmdSize];

        input.Read(partitionHeader.TmdOffset, tmd);

        byte[] cert = new byte[certSize];

        input.Read(certOffset, cert);

        byte[] disc = new byte[FolderLayout.DiscHeadSize];

        input.Read(0, disc);

        var files = FileSystem.Read(reader).Files;
        var reporter = new ProgressReporter(files.Sum(f => f.Size) + dolSize, progress);
        byte[] buffer = new byte[CopyChunkSize];
        bool rootExisted = Directory.Exists(root);
        bool completed = false;

        try
        {
            Directory.CreateDirectory(sysFolder);
            Directory.CreateDirectory(metaFolder);
            File.Delete(FolderInfo.GetPath(root));
            Directory.CreateDirectory(filesFolder);
            File.WriteAllBytes(Path.Combine(sysFolder, FolderLayout.Boot), boot);
            File.WriteAllBytes(Path.Combine(sysFolder, FolderLayout.Bi2), bi2);
            File.WriteAllBytes(Path.Combine(sysFolder, FolderLayout.Apploader), apploader);
            CopyRange(reader, dolOffset, dolSize, Path.Combine(sysFolder, FolderLayout.Dol), buffer, reporter, ct);
            File.WriteAllBytes(Path.Combine(metaFolder, FolderLayout.Disc), disc);
            File.WriteAllBytes(Path.Combine(metaFolder, FolderLayout.Partition), partition);
            File.WriteAllBytes(Path.Combine(metaFolder, FolderLayout.Tmd), tmd);
            File.WriteAllBytes(Path.Combine(metaFolder, FolderLayout.Cert), cert);

            string boundary = Path.GetFullPath(filesFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                string destination = Path.GetFullPath(Path.Combine(filesFolder, file.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

                if (!destination.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"디스크에 안전하지 않은 경로가 있습니다: {file.Path}");

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                CopyRange(reader, file.Offset, file.Size, destination, buffer, reporter, ct);
            }

            string gameId = Encoding.ASCII.GetString(boot, 0, 6);
            string title = Encoding.ASCII.GetString(boot, 0x20, 0x40).TrimEnd('\0').Trim();

            new FolderInfo(FolderInfo.CurrentVersion, target.ContainerOffset, input.Length, gameId, title).Write(root);

            completed = true;

            return files.Count;
        }
        finally
        {
            if (!completed)
                DeleteOutput(root, rootExisted, sysFolder, metaFolder, filesFolder);
        }
    }

    private static void DeleteOutput(string root, bool rootExisted, params string[] folders)
    {
        foreach (string folder in folders)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch { }
        }

        try
        {
            if (!rootExisted && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                Directory.Delete(root);
        }
        catch { }
    }

    private static void CopyRange(PartitionReader reader, long offset, long size, string destination, byte[] buffer, ProgressReporter reporter, CancellationToken ct)
    {
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, CopyChunkSize);
        long remaining = size;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            int count = (int)Math.Min(buffer.Length, remaining);
            var span = buffer.AsSpan(0, count);

            reader.Read(offset, span);
            output.Write(span);

            offset += count;
            remaining -= count;

            reporter.Add(count);
        }
    }
}