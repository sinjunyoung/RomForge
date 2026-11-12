using LibHac.Ns;
using Common.Localization;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.HacPack.Services;

public static class NacpProcessor
{
    const int LegacyLanguageCount = 16;
    const int CompressedSlotCount = 18;
    const int ReservedSlotCount = 32;
    const int LanguageEntrySize = 0x300;
    const int TitleSize = 0x200;
    const int AuthorSize = 0x100;
    const int AuthorOffsetWithinEntry = 0x200;
    const int TitleBlockSize = 0x3000;
    const int SupportedLanguageFlagOffset = 0x302C;

    public static void ProcessControlMetadata(NcaGenerationOptions settings) => ProcessControlMetadata(settings, new DiskFileStore(settings.RomfsDirectory));

    public static void ProcessControlMetadata(NcaGenerationOptions settings, IFileStore store)
    {
        const string controlNacpName = "control.nacp";

        if (!store.Exists(controlNacpName))
            return;

        byte[] nacpRawData = store.ReadAllBytes(controlNacpName);
        var control = MemoryMarshal.Read<ApplicationControlProperty>(nacpRawData);
        bool isCompressed = control.TitleCompression == TitleCompressionValue.Enable;

        if (isCompressed)
        {
            ReadOnlySpan<byte> titleBlock = nacpRawData.AsSpan(0, TitleBlockSize);
            ushort compressedSize = BinaryPrimitives.ReadUInt16LittleEndian(titleBlock);
            ReadOnlySpan<byte> compressedBlob = titleBlock.Slice(2, compressedSize);
            byte[] decompressed = new byte[ReservedSlotCount * LanguageEntrySize];
            using (var ms = new MemoryStream(compressedBlob.ToArray()))
            using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                deflate.ReadExactly(decompressed, 0, decompressed.Length);

            ApplyLanguageChanges(decompressed, isCompressed: true, settings, store);

            byte[] recompressed;
            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal))
                    deflate.Write(decompressed, 0, decompressed.Length);

                recompressed = ms.ToArray();
            }

            if (recompressed.Length + 2 > TitleBlockSize)
                throw new InvalidDataException(Loc.S.ErrRecompressedExceedsTitleBlock(recompressed.Length + 2, TitleBlockSize));

            Array.Clear(nacpRawData, 0, TitleBlockSize);
            BinaryPrimitives.WriteUInt16LittleEndian(nacpRawData, (ushort)recompressed.Length);
            Buffer.BlockCopy(recompressed, 0, nacpRawData, 2, recompressed.Length);
        }
        else
        {
            ValidateLegacyLanguages(settings);
            ApplyLanguageChanges(nacpRawData, isCompressed: false, settings, store);
        }

        ApplyLanguageFlags(nacpRawData, settings);
        store.WriteAllBytes(controlNacpName, nacpRawData);
        VerifyMetadata(nacpRawData, isCompressed);
    }

    private static void ValidateLegacyLanguages(NcaGenerationOptions settings)
    {
        if (settings.UserMetadata == null)
            return;

        foreach (var info in settings.UserMetadata.Languages)
        {
            if ((int)info.Language >= LegacyLanguageCount)
                throw new NotSupportedException(Loc.S.ErrUncompressedNacpLanguageLimit(LegacyLanguageCount - 1, info.Language, (int)info.Language));
        }
    }

    private static void ApplyLanguageChanges(byte[] data, bool isCompressed, NcaGenerationOptions settings, IFileStore store)
    {
        if (settings.UserMetadata == null)
            return;

        int maxSlot = isCompressed ? CompressedSlotCount : LegacyLanguageCount;

        foreach (var info in settings.UserMetadata.Languages)
        {
            int langIndex = (int)info.Language;

            if (langIndex < 0 || langIndex >= maxSlot)
                throw new ArgumentOutOfRangeException(Loc.S.ErrLanguageIndexOutOfRange(langIndex, maxSlot - 1));

            int entryOffset = langIndex * LanguageEntrySize;

            if (!string.IsNullOrEmpty(info.TitleName))
                UpdateNacpString(data, entryOffset, info.TitleName, TitleSize);

            if (!string.IsNullOrEmpty(info.Publisher))
                UpdateNacpString(data, entryOffset + AuthorOffsetWithinEntry, info.Publisher, AuthorSize);

            if (info.LogoData != null && info.LogoData.Length > 0)
            {
                string iconFileName = $"icon_{info.Language}.dat";

                try
                {
                    store.WriteAllBytes(iconFileName, info.LogoData);
                }
                catch (Exception ex)
                {
                    throw new IOException(Loc.S.ErrIconWriteFailed(iconFileName), ex);
                }
            }
        }
    }

    private static void ApplyLanguageFlags(byte[] nacpRawData, NcaGenerationOptions settings)
    {
        if (settings.Language != Language.None)
        {
            uint flag = (uint)(1 << (int)settings.Language);

            BinaryPrimitives.WriteUInt32LittleEndian(nacpRawData.AsSpan(SupportedLanguageFlagOffset), flag);
        }
        else if (settings.UserMetadata != null)
        {
            uint combinedFlag = 0;

            foreach (var info in settings.UserMetadata.Languages)
                if (info.Flag)
                    combinedFlag |= (uint)(1 << (int)info.Language);

            if (combinedFlag != 0)
                BinaryPrimitives.WriteUInt32LittleEndian(nacpRawData.AsSpan(SupportedLanguageFlagOffset), combinedFlag);
        }
    }

    private static void UpdateNacpString(byte[] data, int offset, string value, int maxLength)
    {
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);
        int copyLength = Math.Min(stringBytes.Length, maxLength - 1);

        Array.Clear(data, offset, maxLength);
        Buffer.BlockCopy(stringBytes, 0, data, offset, copyLength);
    }

    private static void VerifyMetadata(byte[] data, bool isCompressed)
    {
        if (isCompressed)
        {
            ushort compressedSize = BinaryPrimitives.ReadUInt16LittleEndian(data);

            if (compressedSize == 0)
                throw new InvalidDataException(Loc.S.ErrCompressedTitleBlockEmpty);

            return;
        }

        for (int i = 0; i < LegacyLanguageCount; i++)
        {
            if (data[i * LanguageEntrySize] != 0)
                return;
        }

        throw new InvalidDataException(Loc.S.ErrNoValidTitleInfoAfterNacpUpdate);
    }
}