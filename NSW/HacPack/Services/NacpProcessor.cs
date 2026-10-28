using LibHac.Ns;
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

    public static void ProcessControlMetadata(NcaGenerationOptions settings)
    {
        string romfsDir = settings.RomfsDirectory;
        string controlNacpPath = Path.Combine(romfsDir, "control.nacp");

        if (!File.Exists(controlNacpPath))
            return;

        byte[] nacpRawData = File.ReadAllBytes(controlNacpPath);
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

            ApplyLanguageChanges(decompressed, isCompressed: true, settings);

            byte[] recompressed;
            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal))
                    deflate.Write(decompressed, 0, decompressed.Length);

                recompressed = ms.ToArray();
            }

            if (recompressed.Length + 2 > TitleBlockSize)
                throw new InvalidDataException($"재압축 결과가 타이틀 블록을 초과합니다: {recompressed.Length + 2} > {TitleBlockSize}");

            Array.Clear(nacpRawData, 0, TitleBlockSize);
            BinaryPrimitives.WriteUInt16LittleEndian(nacpRawData, (ushort)recompressed.Length);
            Buffer.BlockCopy(recompressed, 0, nacpRawData, 2, recompressed.Length);
        }
        else
        {
            ValidateLegacyLanguages(settings);
            ApplyLanguageChanges(nacpRawData, isCompressed: false, settings);
        }

        ApplyLanguageFlags(nacpRawData, settings);
        File.WriteAllBytes(controlNacpPath, nacpRawData);
        VerifyMetadata(nacpRawData, isCompressed);
    }

    private static void ValidateLegacyLanguages(NcaGenerationOptions settings)
    {
        if (settings.UserMetadata == null)
            return;

        foreach (var info in settings.UserMetadata.Languages)
        {
            if ((int)info.Language >= LegacyLanguageCount)
                throw new NotSupportedException($"비압축 NACP는 index 0~{LegacyLanguageCount - 1} 언어만 지원합니다. 요청된 언어: {info.Language} (index {(int)info.Language})");
        }
    }

    private static void ApplyLanguageChanges(byte[] data, bool isCompressed, NcaGenerationOptions settings)
    {
        if (settings.UserMetadata == null) 
            return;

        int maxSlot = isCompressed ? CompressedSlotCount : LegacyLanguageCount;

        foreach (var info in settings.UserMetadata.Languages)
        {
            int langIndex = (int)info.Language;

            if (langIndex < 0 || langIndex >= maxSlot)
                throw new ArgumentOutOfRangeException($"언어 index {langIndex}이 유효 범위(0~{maxSlot - 1})를 벗어납니다.");

            int entryOffset = langIndex * LanguageEntrySize;

            if (!string.IsNullOrEmpty(info.TitleName))
                UpdateNacpString(data, entryOffset, info.TitleName, TitleSize);

            if (!string.IsNullOrEmpty(info.Publisher))
                UpdateNacpString(data, entryOffset + AuthorOffsetWithinEntry, info.Publisher, AuthorSize);

            if (info.LogoData != null && info.LogoData.Length > 0)
            {
                string iconFileName = $"icon_{info.Language}.dat";
                string iconPath = Path.Combine(settings.RomfsDirectory, iconFileName);

                try 
                { 
                    File.WriteAllBytes(iconPath, info.LogoData); 
                }
                catch (Exception ex) 
                { 
                    throw new IOException($"아이콘 파일 작성 실패: {iconFileName}", ex);
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
                throw new InvalidDataException("압축된 타이틀 블록 크기가 0입니다.");

            return;
        }

        for (int i = 0; i < LegacyLanguageCount; i++)
        {
            if (data[i * LanguageEntrySize] != 0)
                return;
        }

        throw new InvalidDataException("NACP 업데이트 후 유효한 타이틀 정보를 찾을 수 없습니다.");
    }
}