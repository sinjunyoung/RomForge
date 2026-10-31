using NSW.HacPack.Models;
using NSW.HacPack.Cryptography.Core;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace NSW.HacPack.Services;

public static class CnmtGenerator
{
    public static void GenerateApplication(string cnmtFilepath, IEnumerable<NcaGenerationOptions> settingsList)
    {
        var context = new CnmtContext();

        foreach (var settings in settingsList)
        {
            TryAddContentRecord(context, settings.ProgramNcaPath, settings.IsProgramNcaValid, 0x1, settings.IdOffset);
            TryAddContentRecord(context, settings.DataNcaPath, settings.IsDataNcaValid, 0x2, settings.IdOffset);
            TryAddContentRecord(context, settings.ControlNcaPath, settings.IsControlNcaValid, 0x3, settings.IdOffset);
            TryAddContentRecord(context, settings.HtmlDocNcaPath, settings.IsHtmlDocNcaValid, 0x4, settings.IdOffset);
            TryAddContentRecord(context, settings.LegalNcaPath, settings.IsLegalNcaValid, 0x5, settings.IdOffset);
        }

        var baseSettings = settingsList.First(s => s.IdOffset == 0);

        context.Header.Type = 0x80;
        context.Header.TitleId = baseSettings.TitleId;
        context.Header.TitleVersion = baseSettings.TitleVersion;
        context.Header.ExtendedHeaderSize = 0x10;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        var extHeader = new CnmtExtendedApplicationHeader
        {
            PatchTitleId = context.Header.TitleId + 0x800
        };

        using var file = CreateFileStream(cnmtFilepath);

        WriteStructure(file, context.Header);
        WriteStructure(file, extHeader);
        WriteContentEntries(file, context);
        file.Write(baseSettings.Digest, 0, 0x20);

        //ExportToXml("cnmt.xml", context, baseSettings.TitleId, baseSettings.TitleVersion, BitConverter.ToString(baseSettings.Digest));
    }

    public static void ExportToXml(string xmlFilepath, CnmtContext context, ulong titleId, uint version, string digest)
    {
        var xml = new XElement("ContentMeta",
            new XElement("Type", "Application"),
            new XElement("Id", "0x" + titleId.ToString("X16")),
            new XElement("Version", version),
            new XElement("RequiredDownloadSystemVersion", 0),

            context.ContentRecords.Take(context.ContentRecordsCount).Select(record =>
                new XElement("Content",
                    new XElement("Type", GetContentTypeName(record.Type)),
                    new XElement("Id", Convert.ToHexStringLower(record.NcaId)),
                    new XElement("IdOffset", record.IdOffset),
                    new XElement("Size", BitConverter.ToUInt64([.. record.Size, .. new byte[2]], 0)),
                    new XElement("Hash", Convert.ToHexStringLower(record.Hash)),
                    new XElement("KeyGeneration", 11)
                )
            ),
            new XElement("Digest", digest),
            new XElement("KeyGenerationMin", 11),
            new XElement("RequiredSystemVersion", 1073741824)
        );

        xml.Save(xmlFilepath);
    }

    private static string GetContentTypeName(byte type) => type switch
    {
        0x1 => "Program",
        0x2 => "Data",
        0x3 => "Control",
        0x4 => "HtmlDocument",
        0x5 => "LegalInformation",
        _ => "Unknown"
    };

    public static void GenerateAddon(string cnmtFilepath, NcaGenerationOptions settings)
    {
        var context = new CnmtContext();

        TryAddContentRecord(context, settings.PublicDataNcaPath, settings.IsPublicDataNcaValid, 0x2);

        context.Header.Type = 0x82;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ExtendedHeaderSize = 0x10;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        var extHeader = new CnmtExtendedAddonHeader
        {
            ApplicationTitleId = context.Header.TitleId - 0x1000 & 0xFFFFFFFFFFFFF000
        };

        using var file = CreateFileStream(cnmtFilepath);

        WriteStructure(file, context.Header);
        WriteStructure(file, extHeader);
        WriteContentEntries(file, context);
        file.Write(settings.Digest, 0, 0x20);
    }

    public static void CreateSystemProgram(string cnmtFilepath, NcaGenerationOptions settings)
    {
        var context = new CnmtContext();

        TryAddContentRecord(context, settings.ProgramNcaPath, settings.IsProgramNcaValid, 0x1);

        context.Header.Type = 0x1;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        using var file = CreateFileStream(cnmtFilepath);

        WriteStructure(file, context.Header);
        WriteContentEntries(file, context);
        file.Write(settings.Digest, 0, 0x20);
    }

    public static void CreateSystemData(string cnmtFilepath, NcaGenerationOptions settings)
    {
        var context = new CnmtContext();

        TryAddContentRecord(context, settings.DataNcaPath, settings.IsDataNcaValid, 0x2);

        context.Header.Type = 0x2;
        context.Header.TitleId = settings.TitleId;
        context.Header.TitleVersion = settings.TitleVersion;
        context.Header.ContentEntryCount = context.ContentRecordsCount;

        using var file = CreateFileStream(cnmtFilepath);

        WriteStructure(file, context.Header);
        WriteContentEntries(file, context);
        file.Write(settings.Digest, 0, 0x20);
    }

    public static void PopulateContentRecord(string ncaPath, ref CnmtContentRecord record)
    {
        using var file = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        byte[] hash = Sha256Service.CalculateHash(file);

        record.Hash ??= new byte[32];
        record.NcaId ??= new byte[16];
        record.Size ??= new byte[6];

        Array.Copy(hash, 0, record.Hash, 0, 32);
        Array.Copy(hash, 0, record.NcaId, 0, 16);

        ulong length = (ulong)file.Length;
        Span<byte> sizeSpan = stackalloc byte[8];

        MemoryMarshal.Write(sizeSpan, in length);
        sizeSpan[..6].CopyTo(record.Size);
    }

    private static void TryAddContentRecord(CnmtContext context, string path, bool isValid, byte type, byte idOffset = 0)
    {
        if (!isValid)
            return;

        PopulateContentRecord(path, ref context.ContentRecords[context.ContentRecordsCount]);

        context.ContentRecords[context.ContentRecordsCount].Type = type;
        context.ContentRecords[context.ContentRecordsCount].IdOffset = idOffset;
        context.ContentRecordsCount++;
    }

    private static FileStream CreateFileStream(string path)
    {
        try 
        { 
            return File.Open(path, FileMode.Create, FileAccess.Write); 
        }
        catch (Exception ex) 
        {
            throw new IOException($"Failed to create {path}!", ex); 
        }
    }

    private static void WriteStructure<T>(FileStream file, T value) where T : struct
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

            file.Write(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void WriteContentEntries(FileStream file, CnmtContext ctx)
    {
        for (int i = 0; i < ctx.ContentRecordsCount; i++)
            WriteStructure(file, ctx.ContentRecords[i]);
    }
}