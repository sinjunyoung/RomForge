using _3DS.Core.Crypto;
using _3DS.Core.FileSystem;
using _3DS.Core.Services;
using Common;
using RomForge.Core;
using RomForge.Core.Models._3DS;
using System.IO;

namespace RomForge.Core.Services._3DS;

internal sealed class RepackOutputBuilder(Action<string, LogLevel> log)
{
    public Task<string> BuildOutputAsync(RepackedNcsdSource repackedSource, string outputBasePath, KeyStore? keyStore, RepackOutputFormat format, byte[]? exHeaderPart0, byte[]? exefsBlockPart0, Action<long, long>? reporter, Action<string>? onOutputPathKnown, CancellationToken ct) => format switch
    {
        RepackOutputFormat.Cia => BuildCiaAsync(repackedSource, outputBasePath, keyStore, exHeaderPart0, exefsBlockPart0, reporter, onOutputPathKnown, ct),
        RepackOutputFormat.Zcci => BuildZcciAsync(repackedSource, outputBasePath, reporter, onOutputPathKnown, ct),
        _ => BuildCciAsync(repackedSource, outputBasePath, reporter, onOutputPathKnown, ct),
    };

    private static async Task<string> BuildCciAsync(RepackedNcsdSource repackedSource, string outputBasePath, Action<long, long>? reporter, Action<string>? onOutputPathKnown, CancellationToken ct)
    {
        string outputCci = Utils.GetUniqueFilePath(Path.ChangeExtension(outputBasePath, RepackOutputFormat.Cci.ToFileExtension()));
        await using var cciStream = File.Open(outputCci, FileMode.Create, FileAccess.ReadWrite);

        onOutputPathKnown?.Invoke(outputCci);

        await NcsdBuilder.BuildAsync(repackedSource, cciStream, reporter, ct);

        return outputCci;
    }

    private async Task<string> BuildCiaAsync(RepackedNcsdSource repackedSource, string outputBasePath, KeyStore? keyStore, byte[]? exHeaderPart0, byte[]? exefsBlockPart0, Action<long, long>? reporter, Action<string>? onOutputPathKnown, CancellationToken ct)
    {
        if (keyStore == null)
            throw new InvalidOperationException("CIA를 생성하려면 키가 필요합니다.");

        string outputCia = Utils.GetUniqueFilePath(Path.ChangeExtension(outputBasePath, RepackOutputFormat.Cia.ToFileExtension()));
        await using var ciaStream = File.Open(outputCia, FileMode.Create, FileAccess.ReadWrite);

        onOutputPathKnown?.Invoke(outputCia);

        byte[]? smdhPart0 = ExtractIcon(exefsBlockPart0);

        await CiaBuilder.BuildAsync(repackedSource, keyStore, ciaStream, exHeaderPart0, smdhPart0, reporter, log, ct);

        return outputCia;
    }

    private async Task<string> BuildZcciAsync(RepackedNcsdSource repackedSource, string outputBasePath, Action<long, long>? reporter, Action<string>? onOutputPathKnown, CancellationToken ct)
    {
        string tempCci = Utils.GetUniqueFilePath(Path.ChangeExtension(outputBasePath, RepackOutputFormat.Cci.ToFileExtension()));

        try
        {
            await using (var cciStream = File.Open(tempCci, FileMode.Create, FileAccess.ReadWrite))
                await NcsdBuilder.BuildAsync(repackedSource, cciStream, reporter, ct);

            var zcciProgress = reporter == null ? null : new Progress<ProgressInfo>(info => reporter(info.Percent, 100));
            string outputZcci = await Z3dsArchiveService.CompressAsync(tempCci, AppConfig.Instance.Azahar.CompressLevel, zcciProgress, log, ct);

            onOutputPathKnown?.Invoke(outputZcci);

            return outputZcci;
        }
        finally
        {
            if (File.Exists(tempCci))
                try { File.Delete(tempCci); } catch { }
        }
    }

    public static byte[]? ExtractIcon(byte[]? exefsBlock)
    {
        if (exefsBlock == null)
            return null;

        const uint smdhMagic = 0x48444D53;
        const int iconSize = 0x36C0;

        for (int i = 0; i <= exefsBlock.Length - 4; i++)
        {
            if (BitConverter.ToUInt32(exefsBlock, i) != smdhMagic)
                continue;

            if (i + iconSize > exefsBlock.Length)
                return null;

            byte[] iconData = new byte[iconSize];
            Array.Copy(exefsBlock, i, iconData, 0, iconSize);

            return iconData;
        }

        return null;
    }

    public static void ApplySmdhToMemory(byte[] exefsBlock, string? gameName, string? publisher, Action<string, LogLevel> log)
    {
        const uint smdhMagic = 0x48444D53;
        const int headerSize = 0x200;
        const int maxEntries = 8;
        const int iconSize = 0x36C0;

        for (int i = 0; i <= exefsBlock.Length - 4; i++)
        {
            if (BitConverter.ToUInt32(exefsBlock, i) != smdhMagic)
                continue;

            byte[] iconData = new byte[iconSize];

            Array.Copy(exefsBlock, i, iconData, 0, iconSize);

            byte[]? overridden = SmdhWriter.ApplyOverride(iconData, gameName, publisher);

            if (overridden == null)
                continue;

            Array.Copy(overridden, 0, exefsBlock, i, iconSize);

            uint dataOffset = (uint)(i - headerSize);
            int entryIndex = -1;

            for (int e = 0; e < maxEntries; e++)
            {
                int entryBase = e * 0x10;
                uint entryOffset = BitConverter.ToUInt32(exefsBlock, entryBase + 8);
                uint entrySize = BitConverter.ToUInt32(exefsBlock, entryBase + 12);

                if (entryOffset == dataOffset && entrySize == iconSize)
                {
                    entryIndex = e;
                    break;
                }
            }

            if (entryIndex < 0)
            {
                log("⚠️ exefs 헤더에서 icon 엔트리를 찾지 못해 해시를 갱신하지 못했습니다.", LogLevel.Error);
                return;
            }

            int hashBase = headerSize - 0x100 + (maxEntries - 1 - entryIndex) * 0x20;
            byte[] newHash = System.Security.Cryptography.SHA256.HashData(overridden);

            Array.Copy(newHash, 0, exefsBlock, hashBase, newHash.Length);

            log("게임명/배급사 정보를 변경했습니다.", LogLevel.Ok);
            return;
        }
    }
}