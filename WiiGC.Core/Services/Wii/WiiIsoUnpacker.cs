using System.Buffers.Binary;
using System.Text;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

public static class WiiIsoUnpacker
{
    private const int CopyChunkSize = 0x100000;
    private const int MaximumHeaderSize = 0x1000000;
    private const int MaximumCertSize = 0x100000;

    public static int Unpack(string inputPath, string outputFolder, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        var target = WiiIsoRebuilder.FindTargets(input, WiiIsoRebuilder.ReadSpecs(input)).FirstOrDefault() ?? throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");

        if (target.ContainerOffset < WiiFolderLayout.DiscHeadSize)
            throw new InvalidDataException("지원하지 않는 Wii 파티션 배치입니다.");

        using var reader = new WiiPartitionReader(input, target);
        string root = Path.GetFullPath(outputFolder);
        string sysFolder = Path.Combine(root, WiiFolderLayout.Sys);
        string metaFolder = Path.Combine(root, WiiFolderLayout.Meta);
        string filesFolder = Path.Combine(root, WiiFolderLayout.Files);
        byte[] boot = new byte[WiiFolderLayout.BootSize];

        reader.Read(0, boot);

        long dolOffset = ReadOffset(boot, 0x420);

        if (dolOffset < WiiFolderLayout.ApploaderOffset + 0x20 || dolOffset > MaximumHeaderSize || dolOffset + WiiDol.HeaderSize > reader.Length)
            throw new InvalidDataException("Wii 파티션 부트 정보가 올바르지 않습니다.");

        byte[] bi2 = new byte[WiiFolderLayout.Bi2Size];

        reader.Read(WiiFolderLayout.BootSize, bi2);

        byte[] apploaderHeader = new byte[0x20];

        reader.Read(WiiFolderLayout.ApploaderOffset, apploaderHeader);

        long apploaderSize = 0x20 + (long)BinaryPrimitives.ReadUInt32BigEndian(apploaderHeader.AsSpan(0x14)) + BinaryPrimitives.ReadUInt32BigEndian(apploaderHeader.AsSpan(0x18));

        if (WiiFolderLayout.ApploaderOffset + apploaderSize > dolOffset)
            throw new InvalidDataException("apploader 크기가 올바르지 않습니다.");

        byte[] apploader = new byte[apploaderSize];

        reader.Read(WiiFolderLayout.ApploaderOffset, apploader);

        byte[] dolHeader = new byte[WiiDol.HeaderSize];

        reader.Read(dolOffset, dolHeader);

        long dolSize = WiiDol.GetSize(dolHeader);

        if (dolOffset + dolSize > reader.Length)
            throw new InvalidDataException("DOL 크기가 올바르지 않습니다.");

        var partitionHeader = WiiPartitionHeader.Read(input, target.ContainerOffset);
        byte[] partition = new byte[WiiFolderLayout.PartitionHeaderSize];

        input.Read(target.ContainerOffset, partition);

        int certSize = (int)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2AC));
        long certOffset = target.ContainerOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(partition.AsSpan(0x2B0)) << 2);

        if (certSize < 0 || certSize > MaximumCertSize || certOffset + certSize > input.Length)
            throw new InvalidDataException("Wii 파티션 인증서 위치가 올바르지 않습니다.");

        byte[] tmd = new byte[partitionHeader.TmdSize];

        input.Read(partitionHeader.TmdOffset, tmd);

        byte[] cert = new byte[certSize];

        input.Read(certOffset, cert);

        byte[] disc = new byte[WiiFolderLayout.DiscHeadSize];

        input.Read(0, disc);

        var files = WiiFileSystem.Read(reader).Files;
        var reporter = new ProgressReporter(files.Sum(f => f.Size) + dolSize, progress);
        byte[] buffer = new byte[CopyChunkSize];

        bool rootExisted = Directory.Exists(root);
        bool completed = false;

        try
        {
            Directory.CreateDirectory(sysFolder);
            Directory.CreateDirectory(metaFolder);
            File.Delete(WiiFolderInfo.GetPath(root));
            Directory.CreateDirectory(filesFolder);
            File.WriteAllBytes(Path.Combine(sysFolder, WiiFolderLayout.Boot), boot);
            File.WriteAllBytes(Path.Combine(sysFolder, WiiFolderLayout.Bi2), bi2);
            File.WriteAllBytes(Path.Combine(sysFolder, WiiFolderLayout.Apploader), apploader);
            CopyRange(reader, dolOffset, dolSize, Path.Combine(sysFolder, WiiFolderLayout.Dol), buffer, reporter, ct);
            File.WriteAllBytes(Path.Combine(metaFolder, WiiFolderLayout.Disc), disc);
            File.WriteAllBytes(Path.Combine(metaFolder, WiiFolderLayout.Partition), partition);
            File.WriteAllBytes(Path.Combine(metaFolder, WiiFolderLayout.Tmd), tmd);
            File.WriteAllBytes(Path.Combine(metaFolder, WiiFolderLayout.Cert), cert);

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

            new WiiFolderInfo(WiiFolderInfo.CurrentVersion, target.ContainerOffset, input.Length, gameId, title).Write(root);

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
            catch
            {
            }
        }

        try
        {
            if (!rootExisted && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
                Directory.Delete(root);
        }
        catch { }
    }

    private static void CopyRange(WiiPartitionReader reader, long offset, long size, string destination, byte[] buffer, ProgressReporter reporter, CancellationToken ct)
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

    private static long ReadOffset(byte[] boot, int position) => (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(position)) << 2;
}