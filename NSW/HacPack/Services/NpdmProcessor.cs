using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Cryptography.Keys;
using NSW.HacPack.Models;
using System.IO;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class NpdmProcessor
{
    private const uint MagicMeta = 0x4154454D;
    private const uint MagicAcid = 0x44494341;
    private const uint MagicAci0 = 0x30494341;

    public static void PatchNpdmMetadata(NcaGenerationOptions settings)
    {
        string npdmPath = Path.Combine(settings.ExefsDirectory, "main.npdm");
        using var fl = File.Open(npdmPath, FileMode.Open, FileAccess.ReadWrite);

        PatchNpdmMetadata(settings, fl);
    }

    public static void PatchNpdmMetadata(NcaGenerationOptions settings, Stream fl)
    {
        NpdmHeader npdm = ReadStructureFromStream<NpdmHeader>(fl);

        if (npdm.Magic != MagicMeta)
            throw new InvalidDataException("Invalid NPDM magic!");

        fl.Seek(npdm.AcidOffset, SeekOrigin.Begin);

        NpdmAcid acid = ReadStructureFromStream<NpdmAcid>(fl);

        if (acid.Magic != MagicAcid)
            throw new InvalidDataException("Invalid ACID magic!");

        fl.Seek(npdm.Aci0Offset, SeekOrigin.Begin);

        NpdmAci0 aci0 = ReadStructureFromStream<NpdmAci0>(fl);

        if (aci0.Magic != MagicAci0)
            throw new InvalidDataException("Invalid ACI0 magic!");

        ulong programTitleId = settings.TitleId + settings.IdOffset;

        if (aci0.TitleId != programTitleId)
        {
            aci0.TitleId = programTitleId;

            fl.Seek(npdm.Aci0Offset, SeekOrigin.Begin);
            WriteStructureToStream(fl, aci0);

            acid.TitleIdRangeMin = programTitleId;
            acid.TitleIdRangeMax = programTitleId;

            fl.Seek(npdm.AcidOffset, SeekOrigin.Begin);
            WriteStructureToStream(fl, acid);
        }

        if (settings.NoSelfSignNcaSignature2 == 0 || !string.IsNullOrEmpty(settings.NcaSignatureModulus))
        {
            fl.Seek(npdm.AcidOffset + 0x100, SeekOrigin.Begin);

            if (!string.IsNullOrEmpty(settings.NcaSignatureModulus) && File.Exists(settings.NcaSignatureModulus))
            {
                byte[] modulus = new byte[0x100];
                using var flModulus = File.OpenRead(settings.NcaSignatureModulus);

                if (flModulus.Read(modulus, 0, 0x100) != 0x100)
                    throw new IOException($"Failed to read nca signature 2 modulus from: {settings.NcaSignatureModulus}");

                fl.Write(modulus, 0, 0x100);
            }
            else
                fl.Write(AcidKeyData.PublicKey, 0, 0x100);
        }

        if (!string.IsNullOrEmpty(settings.AcidSignaturePrivateKey) && File.Exists(settings.AcidSignaturePrivateKey))
        {
            long dataOffset = npdm.AcidOffset + 0x100;

            fl.Seek(dataOffset, SeekOrigin.Begin);

            byte[] acidBuf = new byte[acid.Size];

            fl.ReadExactly(acidBuf);

            byte[] newSignature = new byte[0x100];

            RsaSigner.SignDataWithKeyFile(acidBuf, newSignature, settings.AcidSignaturePrivateKey);
            fl.Seek(npdm.AcidOffset, SeekOrigin.Begin);
            fl.Write(newSignature, 0, 0x100);
        }
    }

    private static T ReadStructureFromStream<T>(Stream stream) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buf = new byte[size];

        if (stream.Read(buf, 0, size) != size)
            throw new IOException($"Failed to read struct {typeof(T).Name}");

        nint ptr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.Copy(buf, 0, ptr, size);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        { 
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void WriteStructureToStream<T>(Stream stream, T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        nint ptr = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(value, ptr, false);

            byte[] buf = new byte[size];

            Marshal.Copy(ptr, buf, 0, size);
            stream.Write(buf, 0, size);
        }
        finally 
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}