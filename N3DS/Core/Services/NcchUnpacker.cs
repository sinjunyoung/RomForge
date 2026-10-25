using N3DS.Core.Models;

namespace N3DS.Core.Services;

public static class NcchUnpacker
{
    private const int MediaUnit = 0x200;

    public static async Task<NcchUnpackResult> UnpackAsync(Stream ncchStream, NcchHeader ncchHeader, CancellationToken ct = default)
    {
        byte[]? exHeader = null;
        byte[]? logo = null;
        byte[]? plainRegion = null;
        ExeFsUnpackResult? exeFs = null;
        RomFsUnpackResult? romFs = null;

        if (ncchHeader.ExtendedHeaderSize > 0)
        {
            exHeader = new byte[0x800];
            ncchStream.Position = 0x200;

            await ncchStream.ReadExactlyAsync(exHeader, ct);
        }

        if (ncchHeader.LogoOffset > 0 && ncchHeader.LogoSize > 0)
        {
            logo = new byte[(long)ncchHeader.LogoSize * MediaUnit];
            ncchStream.Position = (long)ncchHeader.LogoOffset * MediaUnit;

            await ncchStream.ReadExactlyAsync(logo, ct);
        }

        if (ncchHeader.PlainRegionOffset > 0 && ncchHeader.PlainRegionSize > 0)
        {
            plainRegion = new byte[(long)ncchHeader.PlainRegionSize * MediaUnit];
            ncchStream.Position = (long)ncchHeader.PlainRegionOffset * MediaUnit;

            await ncchStream.ReadExactlyAsync(plainRegion, ct);
        }

        if (ncchHeader.ExefsOffset > 0 && ncchHeader.ExefsSize > 0)
            exeFs = await ExeFsUnpacker.UnpackAsync(ncchStream, ncchHeader, ct);

        if (ncchHeader.RomfsOffset > 0 && ncchHeader.RomfsSize > 0)
            romFs = await RomFsUnpacker.UnpackAsync(ncchStream, ncchHeader, ct);

        return new NcchUnpackResult
        {
            Header = ncchHeader,
            ExHeader = exHeader,
            Logo = logo,
            PlainRegion = plainRegion,
            ExeFs = exeFs,
            RomFs = romFs,
        };
    }

    public static async Task SaveToDirectoryAsync(Stream ncchStream, NcchUnpackResult result, string outputDir, Contents? contents = null, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);

        byte[] headerRaw = new byte[NcchHeader.Size];
        using (var ms = new MemoryStream(headerRaw))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(result.Header.Signature);
            bw.Write(result.Header.Magic);
            bw.Write(result.Header.ContentSize);
            bw.Write(result.Header.PartitionId);
            bw.Write(result.Header.MakerCode);
            bw.Write(result.Header.Version);
            bw.Write(result.Header.FirmwareHashMask);
            bw.Write(result.Header.ProgramId);
            bw.Write(result.Header.Reserved1);
            bw.Write(result.Header.LogoHash);
            bw.Write(result.Header.ProductCode);
            bw.Write(result.Header.ExtendedHeaderHash);
            bw.Write(result.Header.ExtendedHeaderSize);
            bw.Write(result.Header.Reserved2);
            bw.Write(result.Header.Flags);
            bw.Write(result.Header.PlainRegionOffset);
            bw.Write(result.Header.PlainRegionSize);
            bw.Write(result.Header.LogoOffset);
            bw.Write(result.Header.LogoSize);
            bw.Write(result.Header.ExefsOffset);
            bw.Write(result.Header.ExefsSize);
            bw.Write(result.Header.ExefsHashSize);
            bw.Write(result.Header.Reserved3);
            bw.Write(result.Header.RomfsOffset);
            bw.Write(result.Header.RomfsSize);
            bw.Write(result.Header.RomfsHashSize);
            bw.Write(result.Header.Reserved4);
            bw.Write(result.Header.ExefsHash);
            bw.Write(result.Header.RomfsHash);
        }

        await File.WriteAllBytesAsync(Path.Combine(outputDir, "header.bin"), headerRaw, ct);

        if (contents != null)
        {
            byte[] contentRaw = new byte[8];

            using var ms = new MemoryStream(contentRaw);
            using var bw = new BinaryWriter(ms);

            bw.Write(contents.ContentId);
            bw.Write(contents.ContentIndex);
            bw.Write(contents.ContentType);

            await File.WriteAllBytesAsync(Path.Combine(outputDir, "content.bin"), contentRaw, ct);
        }

        if (result.ExHeader != null)
            await File.WriteAllBytesAsync(Path.Combine(outputDir, "exheader.bin"), result.ExHeader, ct);

        if (result.Logo != null)
            await File.WriteAllBytesAsync(Path.Combine(outputDir, "logo.bin"), result.Logo, ct);

        if (result.PlainRegion != null)
            await File.WriteAllBytesAsync(Path.Combine(outputDir, "plain.bin"), result.PlainRegion, ct);

        long totalExeFsBytes = (long)result.Header.ExefsSize * MediaUnit;
        long totalRomFsBytes = (long)result.Header.RomfsSize * MediaUnit;
        long totalPartitionBytes = totalExeFsBytes + totalRomFsBytes;
        long accumulatedBytes = 0;
        Action<long, long>? exeFsReporter = null;

        if (reporter != null && totalPartitionBytes > 0)
        {
            long lastExeFsCurrent = 0;

            exeFsReporter = (current, total) =>
            {
                long delta = current - lastExeFsCurrent;

                if (delta > 0)
                {
                    accumulatedBytes += delta;
                    lastExeFsCurrent = current;
                    reporter(accumulatedBytes, totalPartitionBytes);
                }
            };
        }

        if (result.ExeFs != null)
        {
            await ExeFsUnpacker.SaveToDirectoryAsync(result.ExeFs, Path.Combine(outputDir, "exefs"), exeFsReporter, ct);

            accumulatedBytes = totalExeFsBytes;
        }

        Action<long, long>? romFsReporter = null;

        if (reporter != null && totalPartitionBytes > 0)
        {
            long lastRomFsCurrent = 0;

            romFsReporter = (current, total) =>
            {
                long delta = current - lastRomFsCurrent;

                if (delta > 0)
                {
                    accumulatedBytes += delta;
                    lastRomFsCurrent = current;
                    reporter(accumulatedBytes, totalPartitionBytes);
                }
            };
        }

        if (result.RomFs != null)
            await RomFsUnpacker.SaveToDirectoryAsync(ncchStream, result.RomFs, Path.Combine(outputDir, "romfs"), romFsReporter, ct);

        if (reporter != null && totalPartitionBytes > 0)
            reporter(totalPartitionBytes, totalPartitionBytes);
    }
}