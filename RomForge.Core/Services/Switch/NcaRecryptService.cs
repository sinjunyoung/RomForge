using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System.IO;

namespace RomForge.Core.Services.Switch;

public static class NcaRecryptService
{
    public static async Task RecryptAsync(Stream inputStream, Stream outputStream, int newKeyGeneration, KeySet keySet, Action<long>? onRead = null, CancellationToken ct = default)
    {
        var inStorage = new StreamStorage(inputStream, true);
        var nca = new Nca(keySet, inStorage);

        if (nca.Header.KeyGeneration == newKeyGeneration)
        {
            inputStream.Position = 0;

            await Common.Utils.CopyStreamAsync(inputStream, outputStream, onRead, ct);

            return;
        }

        byte[] headerBytes = await GetRecryptedHeaderAsync(inStorage, newKeyGeneration, keySet, ct);

        await outputStream.WriteAsync(headerBytes, ct);

        long totalSize = inputStream.Length;
        long remaining = totalSize - 0xC00;
        long offset = 0xC00;
        const int bufSize = 0x100000;
        byte[] buf = new byte[bufSize];

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            int readSize = (int)Math.Min(bufSize, remaining);

            inStorage.Read(offset, buf.AsSpan(0, readSize)).ThrowIfFailure();
            await outputStream.WriteAsync(buf.AsMemory(0, readSize), ct);
            onRead?.Invoke(readSize);

            offset += readSize;
            remaining -= readSize;
        }

        await outputStream.FlushAsync(ct);
    }

    public static async Task<byte[]> GetRecryptedHeaderAsync(IStorage inStorage, int newKeyGeneration, KeySet keySet, CancellationToken ct = default)
    {
        var nca = new Nca(keySet, inStorage);
        var libHacHdr = nca.Header;
        byte[] headerBytes = new byte[0xC00];

        nca.OpenDecryptedHeaderStorage().Read(0, headerBytes).ThrowIfFailure();
        ct.ThrowIfCancellationRequested();

        if (newKeyGeneration <= 2)
        {
            headerBytes[0x206] = (byte)newKeyGeneration;
            headerBytes[0x220] = 0;
        }
        else
        {
            headerBytes[0x206] = 0x2;
            headerBytes[0x220] = (byte)newKeyGeneration;
        }

        int newKeyGenIdx = Math.Max(newKeyGeneration, 1) - 1;
        bool hasTitleKey = headerBytes.Skip(0x230).Take(0x10).Any(b => b != 0);

        if (hasTitleKey && newKeyGeneration == 0)
            ConvertToTicketless(headerBytes, nca, libHacHdr, keySet, newKeyGenIdx);

        else if (!hasTitleKey)
        {
            int oldKeyGenIdx = Math.Max((int)libHacHdr.KeyGeneration, 1) - 1;
            var oldKaek = keySet.KeyAreaKeys[oldKeyGenIdx][libHacHdr.KeyAreaKeyIndex];
            var newKaek = keySet.KeyAreaKeys[newKeyGenIdx][libHacHdr.KeyAreaKeyIndex];

            for (int k = 0; k < 4; k++)
            {
                ct.ThrowIfCancellationRequested();

                int keyOffset = 0x300 + k * 0x10;
                byte[] key = new byte[0x10];

                Array.Copy(headerBytes, keyOffset, key, 0, 0x10);

                if (key.All(b => b == 0))
                    continue;

                Aes.DecryptEcb128(key.AsSpan(), key.AsSpan(), oldKaek.DataRo);
                Aes.EncryptEcb128(key.AsSpan(), key.AsSpan(), newKaek.DataRo);
                Array.Copy(key, 0, headerBytes, keyOffset, 0x10);
            }
        }


        byte[] key1 = keySet.HeaderKey.SubKeys[0].DataRo.ToArray();
        byte[] key2 = keySet.HeaderKey.SubKeys[1].DataRo.ToArray();
        var transform = new Aes128XtsTransform(key1, key2, false);

        for (int sector = 0; sector < 6; sector++)
        {
            ct.ThrowIfCancellationRequested();
            transform.TransformBlock(headerBytes, sector * 0x200, 0x200, (ulong)sector);
        }

        return headerBytes;
    }

    private static void ConvertToTicketless(byte[] headerBytes, Nca nca, NcaHeader libHacHdr, KeySet keySet, int newKeyGenIdx)
    {
        byte[] titleKey = nca.GetDecryptedTitleKey();
        var newKaek = keySet.KeyAreaKeys[newKeyGenIdx][libHacHdr.KeyAreaKeyIndex];
        byte[] encryptedTitleKey = new byte[0x10];

        Array.Copy(titleKey, encryptedTitleKey, 0x10);
        Aes.EncryptEcb128(encryptedTitleKey.AsSpan(), encryptedTitleKey.AsSpan(), newKaek.DataRo);
        Array.Clear(headerBytes, 0x230, 0x10);
        Array.Clear(headerBytes, 0x300, 0x40);
        Array.Copy(encryptedTitleKey, 0, headerBytes, 0x320, 0x10);
    }
}