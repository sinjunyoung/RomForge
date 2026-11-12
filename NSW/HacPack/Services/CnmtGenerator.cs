using NSW.HacPack.Models;
using Common.Localization;
using System.IO;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class CnmtGenerator
{
    public static byte[] Build(NcaGenerationOptions baseSettings, IReadOnlyList<CnmtContent> contents) => baseSettings.TitleType switch
    {
        LibHac.Ncm.ContentMetaType.Application => BuildApplication(baseSettings, contents),
        LibHac.Ncm.ContentMetaType.AddOnContent => BuildAddon(baseSettings, contents),
        LibHac.Ncm.ContentMetaType.SystemProgram => BuildSystemProgram(baseSettings, contents),
        LibHac.Ncm.ContentMetaType.SystemData => BuildSystemData(baseSettings, contents),
        LibHac.Ncm.ContentMetaType.Patch => throw new NotSupportedException("Creating Patch metadata without providing cnmt is not supported yet!"),
        _ => throw new NotSupportedException($"Unknown title type: {baseSettings.TitleType}")
    };

    public static byte[] BuildApplication(NcaGenerationOptions baseSettings, IReadOnlyList<CnmtContent> contents)
    {
        var context = CreateContext(contents);

        context.Header.Type = 0x80;
        context.Header.TitleId = baseSettings.TitleId;
        context.Header.TitleVersion = baseSettings.TitleVersion;
        context.Header.ExtendedHeaderSize = 0x10;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        var extHeader = new CnmtExtendedApplicationHeader
        {
            PatchTitleId = context.Header.TitleId + 0x800
        };

        using var stream = new MemoryStream();

        WriteStructure(stream, context.Header);
        WriteStructure(stream, extHeader);
        WriteContentEntries(stream, context);
        stream.Write(baseSettings.Digest, 0, 0x20);
        return stream.ToArray();
    }

    public static byte[] BuildAddon(NcaGenerationOptions settings, IReadOnlyList<CnmtContent> contents)
    {
        var context = CreateContext(contents);

        context.Header.Type = 0x82;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ExtendedHeaderSize = 0x10;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        var extHeader = new CnmtExtendedAddonHeader
        {
            ApplicationTitleId = context.Header.TitleId - 0x1000 & 0xFFFFFFFFFFFFF000
        };

        using var stream = new MemoryStream();

        WriteStructure(stream, context.Header);
        WriteStructure(stream, extHeader);
        WriteContentEntries(stream, context);
        stream.Write(settings.Digest, 0, 0x20);

        return stream.ToArray();
    }

    public static byte[] BuildSystemProgram(NcaGenerationOptions settings, IReadOnlyList<CnmtContent> contents)
    {
        var context = CreateContext(contents);

        context.Header.Type = 0x1;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        return Serialize(context, settings);
    }

    public static byte[] BuildSystemData(NcaGenerationOptions settings, IReadOnlyList<CnmtContent> contents)
    {
        var context = CreateContext(contents);

        context.Header.Type = 0x2;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        return Serialize(context, settings);
    }

    private static byte[] Serialize(CnmtContext context, NcaGenerationOptions settings)
    {
        using var stream = new MemoryStream();

        WriteStructure(stream, context.Header);
        WriteContentEntries(stream, context);
        stream.Write(settings.Digest, 0, 0x20);

        return stream.ToArray();
    }

    private static CnmtContext CreateContext(IReadOnlyList<CnmtContent> contents)
    {
        var context = new CnmtContext();

        if (contents.Count > context.ContentRecords.Length)
            throw new InvalidOperationException(Loc.S.ErrTooManyContentRecords(contents.Count));

        Span<byte> sizeSpan = stackalloc byte[8];

        foreach (var content in contents)
        {
            if (content.Hash.Length != 0x20)
                throw new ArgumentException(Loc.S.ErrNcaHashMustBe32Bytes);

            ref var record = ref context.ContentRecords[context.ContentRecordsCount];

            BitConverter.TryWriteBytes(sizeSpan, content.Size);

            record.Hash = (byte[])content.Hash.Clone();
            record.NcaId = content.Hash[..0x10];
            record.Size = sizeSpan[..6].ToArray();
            record.Type = content.Type;
            record.IdOffset = content.IdOffset;
            context.ContentRecordsCount++;
        }

        return context;
    }

    private static void WriteStructure<T>(Stream stream, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        Span<byte> buffer = stackalloc byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(value, ptr, false);

            unsafe
            {
                fixed (byte* pBuffer = buffer)
                    Buffer.MemoryCopy((void*)ptr, pBuffer, size, size);
            }

            stream.Write(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void WriteContentEntries(Stream stream, CnmtContext ctx)
    {
        for (int i = 0; i < ctx.ContentRecordsCount; i++)
            WriteStructure(stream, ctx.ContentRecords[i]);
    }
}