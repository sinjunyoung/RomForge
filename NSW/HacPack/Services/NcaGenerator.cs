using Common.Services;
using Common.Localization;
using Common.Models;
using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Cryptography.Keys;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using System.Buffers;
using System.IO;
using NSW.HacPack.IO;

namespace NSW.HacPack.Services;

public static class NcaGenerator
{
    public static (byte[] Hash, long Size) GenerateRomfsNcaInto(Stream output, NcaGenerationOptions settings, int compressLevel, Action<long>? onAdvance = null, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start, onAdvance);

        WriteRomfsNca(window, settings, compressLevel, progress, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, progress, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static void WriteRomfsNca(Stream ncaFile, NcaGenerationOptions settings, int compressLevel, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);
        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);

        var ivfcStreams = BuildAndWriteRomfsSection(ncaFile, compressLevel, ref ncaHeader, 0, settings, progress, ct);

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
        NcaHeaderService.FinalizeNca(ncaFile, ref ncaHeader, settings);
    }

    public static (byte[] Hash, long Size) GenerateProgramNcaInto(Stream output, NcaGenerationOptions settings, int compressLevel, Action<long>? onAdvance = null, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start, onAdvance);

        WriteProgramNca(window, settings, compressLevel, progress, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, progress, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static byte[] HashRegion(Stream stream, long start, long size, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        const int bufferSize = 4 << 20;

        using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var reporter = new ProgressReporter(Loc.S.ProgressHashingNca, string.Empty, size, progress) { Stage = ProgressStage.Hash };
        byte[][] buffers = [ArrayPool<byte>.Shared.Rent(bufferSize), ArrayPool<byte>.Shared.Rent(bufferSize)];

        try
        {
            stream.Position = start;

            long remaining = size;
            int current = 0;
            int bytes = ReadChunk(stream, buffers[current], remaining);

            while (bytes > 0)
            {
                ct.ThrowIfCancellationRequested();

                int next = current ^ 1;
                long afterCurrent = remaining - bytes;
                var nextRead = afterCurrent > 0 ? Task.Run(() => ReadChunk(stream, buffers[next], afterCurrent)) : Task.FromResult(0);

                hasher.AppendData(buffers[current], 0, bytes);
                reporter.AddProgress(bytes);

                remaining = afterCurrent;
                bytes = nextRead.GetAwaiter().GetResult();
                current = next;
            }

            reporter.ForceReport();

            return hasher.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffers[0]);
            ArrayPool<byte>.Shared.Return(buffers[1]);
        }
    }

    private static int ReadChunk(Stream stream, byte[] buffer, long remaining)
    {
        int wanted = (int)Math.Min(buffer.Length, remaining);
        int total = 0;

        while (total < wanted)
        {
            int read = stream.Read(buffer, total, wanted - total);

            if (read <= 0)
                throw new EndOfStreamException();

            total += read;
        }

        return total;
    }

    private static void WriteProgramNca(Stream ncaFile, NcaGenerationOptions settings, int compressLevel, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);
        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);
        ct.ThrowIfCancellationRequested();

        using var exefsStream = new MemoryStream(10 * 1024 * 1024);
        using var exefsHashStream = new MemoryStream();
        const uint exefsBlockSize = Pfs0Builder.ExefsHashBlockSize;

        BuildPfs0ToStream(settings.ExefsDirectory, settings.ExefsFiles, exefsStream, ct);
        Pfs0Builder.GeneratePfs0HashTable(exefsStream, exefsHashStream, exefsBlockSize, out ulong hashTableSize, out ulong pfs0Offset);

        ulong pfs0Size = (ulong)exefsStream.Length;

        NcaHeaderService.SetPfs0Superblock(ref ncaHeader, 0, pfs0Size, hashTableSize, pfs0Offset, exefsBlockSize);

        using (var exefsSection = EncryptedSectionStream.Create(ncaFile, ref ncaHeader, 0, settings))
        {
            exefsHashStream.Position = 0;

            exefsHashStream.CopyTo(exefsSection);

            exefsStream.Position = 0;

            exefsStream.CopyTo(exefsSection);
            WritePadding(exefsSection);
        }

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
            var ivfcStreams1 = BuildAndWriteRomfsSection(ncaFile, compressLevel, ref ncaHeader, 1, settings, progress, ct);

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

    public static (byte[] Hash, long Size) GenerateMetaNcaInto(Stream output, NcaGenerationOptions settings, byte[] cnmt, CancellationToken ct = default)
    {
        long start = output.Position;
        var window = new OffsetStream(output, start);
        var metaFiles = new List<(string Name, byte[] Data)> { ($"{GetCnmtPrefix(settings.TitleType)}_{settings.TitleId:x16}.cnmt", cnmt) };

        WriteMetaNca(window, settings, metaFiles, ct);

        long size = window.Length;
        byte[] hash = HashRegion(output, start, size, null, ct);

        output.Position = start + size;

        return (hash, size);
    }

    private static void WriteMetaNca(Stream ncaFile, NcaGenerationOptions baseSettings, List<(string Name, byte[] Data)> metaFiles, CancellationToken ct)
    {
        var ncaHeader = new NcaHeader();

        NcaHeaderService.InitHeader(ref ncaHeader);
        NcaStructHelper.WriteStruct(ncaFile, ncaHeader);
        ct.ThrowIfCancellationRequested();

        const uint metaBlockSize = Pfs0Builder.MetaHashBlockSize;
        using var metaPfs0Stream = new MemoryStream();
        using var metaHashStream = new MemoryStream();

        BuildPfs0ToStream(string.Empty, metaFiles, metaPfs0Stream, ct);
        Pfs0Builder.GeneratePfs0HashTable(metaPfs0Stream, metaHashStream, metaBlockSize, out ulong metaHashSize, out ulong metaPfs0Offset);

        ulong metaPfs0Size = (ulong)metaPfs0Stream.Length;

        NcaHeaderService.SetPfs0Superblock(ref ncaHeader, 0, metaPfs0Size, metaHashSize, metaPfs0Offset, metaBlockSize);

        using (var metaSection = EncryptedSectionStream.Create(ncaFile, ref ncaHeader, 0, baseSettings))
        {
            metaHashStream.Position = 0;

            metaHashStream.CopyTo(metaSection);

            metaPfs0Stream.Position = 0;

            metaPfs0Stream.CopyTo(metaSection);
            WritePadding(metaSection);
        }

        NcaHeaderService.SetCommonHeader(ref ncaHeader, baseSettings);

        ncaHeader.ContentType = 0x1;

        NcaHeaderService.SetSectionEntry(ref ncaHeader, 0, 0x6, (uint)(ncaFile.Position / 0x200));
        NcaHeaderService.SetFsHeader(ref ncaHeader, 0, (byte)SectionHashType.Pfs0, (byte)SectionFsType.Pfs0, 0x2, baseSettings.Plaintext == 0 ? (byte)SectionCryptType.Ctr : (byte)SectionCryptType.None);

        byte[] masterHash = Pfs0Builder.GetRootHash(metaHashStream, metaHashSize);

        NcaHeaderService.SetPfs0MasterHash(ref ncaHeader, 0, masterHash);

        byte[] sectionHash = NcaStructHelper.CalculateSectionHash(ref ncaHeader, 0);

        NcaHeaderService.SetSectionHash(ref ncaHeader, 0, sectionHash);

        if (baseSettings.KeyAreaKey == null)
            throw new Exception(Loc.S.ErrKeyAreaKeyNotSet);

        Array.Copy(baseSettings.KeyAreaKey, 0, ncaHeader.EncryptedKeys, 2 * 0x10, 0x10);
        ct.ThrowIfCancellationRequested();
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

    private static MemoryStream[] BuildAndWriteRomfsSection(Stream ncaFile, int compressLevel, ref NcaHeader ncaHeader, int sectionIndex, NcaGenerationOptions settings, IProgress<ProgressInfo>? progress, CancellationToken ct)
        => settings.CompressRomfs ? StreamingRomfsSectionWriter.Write(ncaFile, compressLevel, ref ncaHeader, sectionIndex, settings, progress, ct) : UncompressedRomfsSectionWriter.Write(ncaFile, ref ncaHeader, sectionIndex, settings, progress, ct);

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
}