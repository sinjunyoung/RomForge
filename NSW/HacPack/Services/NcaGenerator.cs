using Common;
using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Cryptography.Keys;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using System.IO;

namespace NSW.HacPack.Services;

public static class NcaGenerator
{
    public static string GenerateRomfsNca(NcaGenerationOptions settings, string ncaType, int compressLevel, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string ncaPath = Path.Combine(settings.OutDirectory, $"{ncaType}.nca");
        using var ncaFile = File.Open(ncaPath, FileMode.Create, FileAccess.ReadWrite);

        WriteRomfsNca(ncaFile, settings, ncaType, compressLevel, progress, ct);

        return WriteAndRename(ncaFile, settings, ncaPath, ".nca");
    }

    public static (byte[] Hash, long Size) GenerateRomfsNcaInto(Stream output, NcaGenerationOptions settings, string ncaType, int compressLevel, Action<long>? onAdvance = null, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start, onAdvance);

        WriteRomfsNca(window, settings, ncaType, compressLevel, null, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static void WriteRomfsNca(Stream ncaFile, NcaGenerationOptions settings, string ncaType, int compressLevel, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);

        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);

        var ivfcStreams = BuildAndWriteRomfsSection(ncaFile, compressLevel, ref ncaHeader, 0, settings, $"{ncaType}_romfs_tmp", progress, ct);

        try
        {
            NcaHeaderService.SetCommonHeader(ref ncaHeader, settings);

            ncaHeader.ContentType = (byte)settings.NcaType;

            NcaHeaderService.SetSectionEntry(ref ncaHeader, 0, 0x6, (uint)(ncaFile.Position / 0x200));
            NcaHeaderService.SetFsHeader(ref ncaHeader, 0, (byte)SectionHashType.RomFs, 0, 0x2, settings.Plaintext == 0 ? (byte)SectionCryptType.Ctr : (byte)SectionCryptType.None);
            NcaHeaderService.SetIvfcMagic(ref ncaHeader, 0);

            byte[] masterHash = IvfcGenerator.CalculateMasterHash(ivfcStreams[0]);

            NcaHeaderService.SetIvfcMasterHash(ref ncaHeader, 0, masterHash);

            byte[] sectionHash = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 0);

            NcaHeaderService.SetSectionHash(ref ncaHeader, 0, sectionHash);
        }
        finally
        {
            foreach (var s in ivfcStreams)
                s?.Dispose();
        }

        NcaHeaderService.FinalizeKeyArea(ref ncaHeader, settings);

        if (settings.Plaintext == 0)
            NcaCryptoService.EncryptSection(ncaFile, ref ncaHeader, 0, settings, progress, ct);

        NcaHeaderService.FinalizeNca(ncaFile, ref ncaHeader, settings);
    }

    public static string GenerateProgramNca(NcaGenerationOptions settings, int compressLevel, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        string ncaPath = Path.Combine(settings.OutDirectory, "Program.nca");
        using var ncaFile = new FileStream(ncaPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);

        WriteProgramNca(ncaFile, settings, compressLevel, progress, ct);

        return WriteAndRename(ncaFile, settings, ncaPath, ".nca");
    }

    public static (byte[] Hash, long Size) GenerateProgramNcaInto(Stream output, NcaGenerationOptions settings, int compressLevel, Action<long>? onAdvance = null, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start, onAdvance);

        WriteProgramNca(window, settings, compressLevel, null, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static byte[] HashRegion(Stream stream, long start, long size, CancellationToken ct)
    {
        using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1 << 20];
        long remaining = size;

        stream.Position = start;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));

            if (read <= 0)
                throw new EndOfStreamException();

            hasher.AppendData(buffer, 0, read);

            remaining -= read;
        }

        return hasher.GetHashAndReset();
    }

    private static void WriteProgramNca(Stream ncaFile, NcaGenerationOptions settings, int compressLevel, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);

        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);
        ct.ThrowIfCancellationRequested();
        progress.ReportStatus("ExeFS 빌드 중...", 0);

        using var exefsStream = new MemoryStream(10 * 1024 * 1024);
        using var exefsHashStream = new MemoryStream();
        const uint exefsBlockSize = Pfs0Builder.ExefsHashBlockSize;

        BuildPfs0ToStream(settings.ExefsDirectory, settings.ExefsFiles, exefsStream, ct);
        Pfs0Builder.GeneratePfs0HashTable(exefsStream, exefsHashStream, exefsBlockSize, out ulong hashTableSize, out ulong pfs0Offset);

        ulong pfs0Size = (ulong)exefsStream.Length;

        NcaHeaderService.SetPfs0Superblock(ref ncaHeader, 0, pfs0Size, hashTableSize, pfs0Offset, exefsBlockSize);

        exefsHashStream.Position = 0;

        exefsHashStream.CopyTo(ncaFile);

        exefsStream.Position = 0;

        exefsStream.CopyTo(ncaFile);
        WritePadding(ncaFile);
        NcaHeaderService.SetCommonHeader(ref ncaHeader, settings);

        ncaHeader.ContentType = 0x0;

        NcaHeaderService.SetSectionEntry(ref ncaHeader, 0, 0x6, (uint)(ncaFile.Position / 0x200));
        NcaHeaderService.SetFsHeader(ref ncaHeader, 0, (byte)SectionHashType.Pfs0, (byte)SectionFsType.Pfs0, 0x2, settings.Plaintext == 0 ? (byte)SectionCryptType.Ctr : (byte)SectionCryptType.None);

        byte[] masterHash0 = Pfs0Builder.GetRootHash(exefsHashStream, hashTableSize);

        NcaHeaderService.SetPfs0MasterHash(ref ncaHeader, 0, masterHash0);

        byte[] sectionHash0 = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 0);

        NcaHeaderService.SetSectionHash(ref ncaHeader, 0, sectionHash0);

        if (settings.HasRomfsSource)
        {
            var ivfcStreams1 = BuildAndWriteRomfsSection(ncaFile, compressLevel, ref ncaHeader, 1, settings, "program_sec1_romfs_tmp", progress, ct);

            try
            {
                uint sec0End = ncaHeader.SectionEntries[0].MediaEndOffset;

                NcaHeaderService.SetSectionEntry(ref ncaHeader, 1, sec0End, (uint)(ncaFile.Position / 0x200));
                NcaHeaderService.SetFsHeader(ref ncaHeader, 1, (byte)SectionHashType.RomFs, 0, 0x2, settings.Plaintext == 0 ? (byte)SectionCryptType.Ctr : (byte)SectionCryptType.None);
                NcaHeaderService.SetIvfcMagic(ref ncaHeader, 1);

                byte[] masterHash1 = IvfcGenerator.CalculateMasterHash(ivfcStreams1[0]);

                NcaHeaderService.SetIvfcMasterHash(ref ncaHeader, 1, masterHash1);

                byte[] sectionHash1 = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 1);

                NcaHeaderService.SetSectionHash(ref ncaHeader, 1, sectionHash1);
            }
            finally
            {
                foreach (var s in ivfcStreams1)
                    s?.Dispose();
            }
        }

        if (settings.LogoFiles != null || (!string.IsNullOrEmpty(settings.LogoDirectory) && Directory.Exists(settings.LogoDirectory)))
        {
            const uint logoBlockSize = Pfs0Builder.LogoHashBlockSize;

            ct.ThrowIfCancellationRequested();

            using var logoStream = new MemoryStream();
            using var logoHashStream = new MemoryStream();

            BuildPfs0ToStream(settings.LogoDirectory, settings.LogoFiles, logoStream, ct);
            Pfs0Builder.GeneratePfs0HashTable(logoStream, logoHashStream, logoBlockSize, out ulong logoHashSize, out ulong logoPfs0Offset);

            ulong logoSize = (ulong)logoStream.Length;

            NcaHeaderService.SetPfs0Superblock(ref ncaHeader, 2, logoSize, logoHashSize, logoPfs0Offset, logoBlockSize);

            logoHashStream.Position = 0;

            logoHashStream.CopyTo(ncaFile);

            logoStream.Position = 0;

            logoStream.CopyTo(ncaFile);

            WritePadding(ncaFile);

            bool hasRomfs = settings.HasRomfsSource;
            uint sec2Start = hasRomfs ? ncaHeader.SectionEntries[1].MediaEndOffset : ncaHeader.SectionEntries[0].MediaEndOffset;

            NcaHeaderService.SetSectionEntry(ref ncaHeader, 2, sec2Start, (uint)(ncaFile.Position / 0x200));
            NcaHeaderService.SetFsHeader(ref ncaHeader, 2, (byte)SectionHashType.Pfs0, (byte)SectionFsType.Pfs0, 0x2, 0x1);

            byte[] masterHash2 = Pfs0Builder.GetRootHash(logoHashStream, logoHashSize);

            NcaHeaderService.SetPfs0MasterHash(ref ncaHeader, 2, masterHash2);

            byte[] sectionHash2 = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 2);

            NcaHeaderService.SetSectionHash(ref ncaHeader, 2, sectionHash2);
        }

        NcaHeaderService.FinalizeKeyArea(ref ncaHeader, settings);
        ct.ThrowIfCancellationRequested();

        if (settings.Plaintext == 0)
        {
            progress.ReportStatus("ExeFS 암호화 중...", 0);
            NcaCryptoService.EncryptSection(ncaFile, ref ncaHeader, 0, settings, progress, ct);

            if (settings.HasRomfsSource)
            {
                ct.ThrowIfCancellationRequested();
                progress.ReportStatus("RomFS 암호화 중...", 0);
                NcaCryptoService.EncryptSection(ncaFile, ref ncaHeader, 1, settings, progress, ct);
            }
        }

        if (settings.NoSelfSignNcaSignature2 == 0 || !string.IsNullOrEmpty(settings.NcaSignature2PrivateKey) && File.Exists(settings.NcaSignature2PrivateKey))
        {
            byte[] headerBytes = NcaStructHelper.GetHeaderMagicBytes(ref ncaHeader);

            if (!string.IsNullOrEmpty(settings.NcaSignature2PrivateKey) && File.Exists(settings.NcaSignature2PrivateKey))
                RsaSigner.SignDataWithKeyFile(headerBytes, ncaHeader.NpdmKeySig, settings.NcaSignature2PrivateKey);
            else
                RsaSigner.GeneratePssSignature(headerBytes, ncaHeader.NpdmKeySig, AcidKeyData.PrivateKeyPem);
        }

        NcaHeaderService.FinalizeNca(ncaFile, ref ncaHeader, settings);
    }

    public static string GenerateMetaNca(IEnumerable<NcaGenerationOptions> settingsList, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var baseSettings = settingsList.First(s => s.IdOffset == 0);
        string ncaPath = Path.Combine(baseSettings.OutDirectory, "Meta.nca");
        using var ncaFile = File.Open(ncaPath, FileMode.Create, FileAccess.ReadWrite);
        string cnmtDirPath = Path.Combine(baseSettings.TempDirectory, "cnmt");

        Directory.CreateDirectory(cnmtDirPath);

        ct.ThrowIfCancellationRequested();
        BuildMetadataFile(cnmtDirPath, settingsList);

        var metaFiles = Directory.GetFiles(cnmtDirPath).Select(f => (Path.GetFileName(f), File.ReadAllBytes(f))).ToList();

        WriteMetaNca(ncaFile, baseSettings, metaFiles, progress, ct);

        byte[] ncaHash = NcaStructHelper.CalculateNcaHash(ncaFile);

        ncaFile.Close();

        string finalName = Convert.ToHexString(ncaHash, 0, 16).ToLowerInvariant() + ".cnmt.nca";
        string finalPath = Path.Combine(baseSettings.OutDirectory, finalName);

        File.Move(ncaPath, finalPath, overwrite: true);

        return finalPath;
    }

    public static (byte[] Hash, long Size) GenerateMetaNcaInto(Stream output, NcaGenerationOptions settings, byte[] cnmt, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start);
        var metaFiles = new List<(string Name, byte[] Data)> { ($"{GetCnmtPrefix(settings.TitleType)}_{settings.TitleId:x16}.cnmt", cnmt) };

        WriteMetaNca(window, settings, metaFiles, null, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static void WriteMetaNca(Stream ncaFile, NcaGenerationOptions baseSettings, List<(string Name, byte[] Data)> metaFiles, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);

        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);
        ct.ThrowIfCancellationRequested();
        progress.ReportStatus("Meta NCA 생성 중...", 0);

        const uint metaBlockSize = Pfs0Builder.MetaHashBlockSize;
        using var metaPfs0Stream = new MemoryStream();
        using var metaHashStream = new MemoryStream();

        BuildPfs0ToStream(string.Empty, metaFiles, metaPfs0Stream, ct);
        Pfs0Builder.GeneratePfs0HashTable(metaPfs0Stream, metaHashStream, metaBlockSize, out ulong metaHashSize, out ulong metaPfs0Offset);

        ulong metaPfs0Size = (ulong)metaPfs0Stream.Length;

        NcaHeaderService.SetPfs0Superblock(ref ncaHeader, 0, metaPfs0Size, metaHashSize, metaPfs0Offset, metaBlockSize);

        metaHashStream.Position = 0;

        metaHashStream.CopyTo(ncaFile);

        metaPfs0Stream.Position = 0;

        metaPfs0Stream.CopyTo(ncaFile);

        WritePadding(ncaFile);

        NcaHeaderService.SetCommonHeader(ref ncaHeader, baseSettings);

        ncaHeader.ContentType = 0x1;

        NcaHeaderService.SetSectionEntry(ref ncaHeader, 0, 0x6, (uint)(ncaFile.Position / 0x200));
        NcaHeaderService.SetFsHeader(ref ncaHeader, 0, (byte)SectionHashType.Pfs0, (byte)SectionFsType.Pfs0, 0x2, baseSettings.Plaintext == 0 ? (byte)SectionCryptType.Ctr : (byte)SectionCryptType.None);

        byte[] masterHash = Pfs0Builder.GetRootHash(metaHashStream, metaHashSize);

        NcaHeaderService.SetPfs0MasterHash(ref ncaHeader, 0, masterHash);

        byte[] sectionHash = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 0);

        NcaHeaderService.SetSectionHash(ref ncaHeader, 0, sectionHash);

        if (baseSettings.KeyAreaKey == null)
            throw new Exception("KeyAreaKey가 설정되지 않았습니다.");

        Array.Copy(baseSettings.KeyAreaKey, 0, ncaHeader.EncryptedKeys, 2 * 0x10, 0x10);

        ct.ThrowIfCancellationRequested();

        if (baseSettings.Plaintext == 0)
            NcaCryptoService.EncryptSection(ncaFile, ref ncaHeader, 0, baseSettings, null, ct);

        ncaFile.Seek(0, SeekOrigin.End);

        ncaHeader.NcaSize = (ulong)ncaFile.Position;

        NcaCryptoService.EncryptKeyArea(ref ncaHeader, baseSettings);
        NcaSigningService.SignNca(ref ncaHeader, baseSettings);
        NcaCryptoService.EncryptHeader(ref ncaHeader, baseSettings);

        ncaFile.Seek(0, SeekOrigin.Begin);
        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);
    }

    private static string GetCnmtPrefix(LibHac.Ncm.ContentMetaType titleType) => titleType switch
    {
        LibHac.Ncm.ContentMetaType.Application => "Application",
        LibHac.Ncm.ContentMetaType.AddOnContent => "AddOnContent",
        LibHac.Ncm.ContentMetaType.SystemProgram => "SystemProgram",
        LibHac.Ncm.ContentMetaType.SystemData => "SystemData",
        LibHac.Ncm.ContentMetaType.Patch => "Patch",
        _ => throw new NotSupportedException($"Unknown title type: {titleType}")
    };

    private static MemoryStream[] BuildAndWriteRomfsSection(Stream ncaFile, int compressLevel, ref NcaHeader ncaHeader, int sectionIndex, NcaGenerationOptions settings, string tempFileName, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (settings.RomfsImageOpener != null)
            return StreamingRomfsSectionWriter.Write(ncaFile, compressLevel, ref ncaHeader, sectionIndex, settings, progress, ct);

        var ivfcStreams = new MemoryStream[5];

        for (int i = 0; i < 5; i++)
            ivfcStreams[i] = new MemoryStream();

        string romfsTempPath = Path.Combine(settings.TempDirectory, tempFileName);
        string compressedTempPath = romfsTempPath + "_cmp";
        using var romfsTempFile = new FileStream(romfsTempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
        FileStream? compressedTempFile = null;

        try
        {
            RomfsBuilder.BuildRomfsImage(settings.RomfsDirectory, romfsTempFile, out ulong romfsSize, progress, ct);

            FileStream dataFile = romfsTempFile;
            ulong dataSize = romfsSize;

            if (settings.CompressRomfs)
            {
                compressedTempFile = new FileStream(compressedTempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);

                var info = CompressedRomfsWriter.Compress(romfsTempFile, compressedTempFile, compressLevel, progress, ct);

                NcaHeaderService.SetCompressionInfo(ref ncaHeader, sectionIndex, info.TableOffset, info.TableSize, info.TableHeader);

                dataFile = compressedTempFile;
                dataSize = (ulong)compressedTempFile.Length;
            }

            NcaHeaderService.SetIvfcLevel5(ref ncaHeader, sectionIndex, dataSize);

            dataFile.Position = 0;

            IvfcGenerator.GenerateHashLevel(ivfcStreams[4], dataFile, out ulong lvlSize4);
            NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, 4, lvlSize4);

            for (int b = 3; b >= 0; b--)
            {
                ct.ThrowIfCancellationRequested();
                IvfcGenerator.GenerateHashLevel(ivfcStreams[b], ivfcStreams[b + 1], out ulong lvlSize);
                NcaHeaderService.SetIvfcLevel(ref ncaHeader, sectionIndex, b, lvlSize);
                progress.ReportStatus($"IVFC 생성 중... {4 - b}/5", -1);
            }

            NcaHeaderService.SetIvfcLogicalOffsets(ref ncaHeader, sectionIndex);

            for (int c = 0; c < 5; c++)
            {
                ct.ThrowIfCancellationRequested();

                ivfcStreams[c].Position = 0;

                ivfcStreams[c].CopyTo(ncaFile);
                progress.ReportStatus($"NCA 기록 중... {c + 1}/6", -1);
            }

            ct.ThrowIfCancellationRequested();

            dataFile.Position = 0;

            dataFile.CopyTo(ncaFile);
            progress.ReportStatus("NCA 기록 중... 6/6", -1);
            WritePadding(ncaFile);

            return ivfcStreams;
        }
        catch
        {
            foreach (var s in ivfcStreams)
                s?.Dispose();

            throw;
        }
        finally
        {
            romfsTempFile.Close();
            compressedTempFile?.Dispose();
            File.Delete(romfsTempPath);

            if (File.Exists(compressedTempPath))
                File.Delete(compressedTempPath);
        }
    }

    private static void BuildPfs0ToStream(string inDirPath, List<(string Name, byte[] Data)>? files, MemoryStream outStream, CancellationToken ct)
    {
        var fileStreams = new List<(string, Stream)>();

        try
        {
            if (files != null)
            {
                foreach (var (name, data) in files.OrderBy(f => f.Name, StringComparer.Ordinal))
                    fileStreams.Add((name, new MemoryStream(data, false)));
            }
            else
            {
                foreach (var f in Directory.GetFiles(inDirPath).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
                    fileStreams.Add((Path.GetFileName(f), File.OpenRead(f)));
            }

            Pfs0Builder.BuildFromMemoryStreams(fileStreams, outStream, null, ct);
        }
        finally
        {
            foreach (var (_, s) in fileStreams)
                s.Dispose();
        }
    }

    public static void WritePadding(Stream ncaFile)
    {
        ulong currOffset = (ulong)ncaFile.Position;

        if (currOffset % 0x200 != 0)
            ncaFile.Write(new byte[0x200 - currOffset % 0x200]);
    }

    private static string WriteAndRename(FileStream ncaFile, NcaGenerationOptions settings, string ncaPath, string ext)
    {
        byte[] ncaHash = NcaStructHelper.CalculateNcaHash(ncaFile);
        string finalName = Convert.ToHexString(ncaHash, 0, 16).ToLowerInvariant() + ext;
        string finalPath = Path.Combine(settings.OutDirectory, finalName);

        ncaFile.Close();
        File.Move(ncaPath, finalPath, overwrite: true);

        return finalPath;
    }

    private static string BuildMetadataFile(string cnmtDirPath, IEnumerable<NcaGenerationOptions> settingsList)
    {
        var baseSettings = settingsList.First(s => s.IdOffset == 0);

        var (prefix, generate) = baseSettings.TitleType switch
        {
            LibHac.Ncm.ContentMetaType.Application => ("Application", (Action<string, IEnumerable<NcaGenerationOptions>>)CnmtGenerator.GenerateApplication),
            LibHac.Ncm.ContentMetaType.AddOnContent => ("AddOnContent", (path, list) => CnmtGenerator.GenerateAddon(path, list.First())),
            LibHac.Ncm.ContentMetaType.SystemProgram => ("SystemProgram", (path, list) => CnmtGenerator.CreateSystemProgram(path, list.First())),
            LibHac.Ncm.ContentMetaType.SystemData => ("SystemData", (path, list) => CnmtGenerator.CreateSystemData(path, list.First())),
            LibHac.Ncm.ContentMetaType.Patch => ("Patch", null!),
            _ => throw new NotSupportedException($"Unknown title type: {baseSettings.TitleType}")
        };

        string cnmtPath = Path.Combine(cnmtDirPath, $"{prefix}_{baseSettings.TitleId:x16}.cnmt");

        if (!string.IsNullOrEmpty(baseSettings.CnmtPath) && File.Exists(baseSettings.CnmtPath))
            File.Copy(baseSettings.CnmtPath, cnmtPath, true);
        else if (generate != null)
            generate(cnmtPath, settingsList);
        else
            throw new NotSupportedException("Creating Patch metadata without providing cnmt is not supported yet!");

        return cnmtPath;
    }
}