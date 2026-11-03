using LibHac.Ncm;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using System.IO;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.HacPack.Services;

public class NcaGenerationOptions
{
    public LibHac.Common.Keys.KeySet KeySet = new();

    public ulong TitleId;

    public string OutDirectory = string.Empty;

    public string ExefsDirectory = string.Empty;

    public string RomfsDirectory = string.Empty;

    public string LogoDirectory = string.Empty;

    public List<(string Name, byte[] Data)>? ExefsFiles;

    public List<(string Name, byte[] Data)>? LogoFiles;

    public string NcaDirectory = string.Empty;

    public string BackupDirectory = string.Empty;

    public string AcidSignaturePrivateKey = string.Empty;

    public string NcaSignature1PrivateKey = string.Empty;

    public string NcaSignature2PrivateKey = string.Empty;

    public string NcaSignatureModulus = string.Empty;

    public byte Plaintext;

    public byte[] Digest = new byte[0x20];

    public uint TitleVersion;

    public byte HasTitleKey;

    public byte NoSelfSignNcaSignature2;

    public byte[] TitleKey = new byte[0x10];

    public byte[]? KeyAreaKey;

    public int KeyGeneration;

    public Language Language = Language.None;

    public uint SdkVersion;

    public byte IdOffset = 0;

    public byte SourceIdOffset = 0;

    public bool CompressRomfs;

    public Func<Stream>? RomfsImageOpener;

    public Stream OpenRomfsImage() => RomfsImageOpener != null ? RomfsImageOpener() : RomfsBuilder.OpenImage(RomfsBuilder.FromDirectory(RomfsDirectory), RomfsDirectory);

    public bool HasRomfsSource => RomfsImageOpener != null || (!string.IsNullOrEmpty(RomfsDirectory) && Directory.Exists(RomfsDirectory));

    public string SdkVersionString
    {
        get
        {
            uint v = SdkVersion;
            return $"{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}";
        }
    }

    public LibHac.FsSystem.NcaHeader.ContentType NcaType = LibHac.FsSystem.NcaHeader.ContentType.Program;

    public FileType FileType = FileType.Nca;

    public ContentMetaType TitleType = ContentMetaType.Application;

    public NcaSigType NcaSig = NcaSigType.Zero;

    public LibHac.FsSystem.NcaHeader.DistributionType NcaDistType = LibHac.FsSystem.NcaHeader.DistributionType.Download;

    public GameMetadata? UserMetadata { get; set; }


    public NcaGenerationOptions WithRomfs(string romfsDir, LibHac.FsSystem.NcaHeader.ContentType ncaType, Func<Stream>? romfsImageOpener = null)
    => new()
    {
        KeySet = KeySet,
        TitleId = TitleId,
        OutDirectory = OutDirectory,
        ExefsDirectory = ExefsDirectory,
        RomfsDirectory = romfsDir,
        RomfsImageOpener = romfsImageOpener,
        LogoDirectory = LogoDirectory,
        ExefsFiles = ExefsFiles,
        LogoFiles = LogoFiles,
        NcaDirectory = NcaDirectory,
        BackupDirectory = BackupDirectory,
        AcidSignaturePrivateKey = AcidSignaturePrivateKey,
        NcaSignature1PrivateKey = NcaSignature1PrivateKey,
        NcaSignature2PrivateKey = NcaSignature2PrivateKey,
        NcaSignatureModulus = NcaSignatureModulus,
        Plaintext = Plaintext,
        Digest = (byte[])Digest.Clone(),
        TitleVersion = TitleVersion,
        HasTitleKey = HasTitleKey,
        NoSelfSignNcaSignature2 = NoSelfSignNcaSignature2,
        TitleKey = (byte[])TitleKey.Clone(),
        KeyAreaKey = KeyAreaKey,
        KeyGeneration = KeyGeneration,
        Language = Language,
        SdkVersion = SdkVersion,
        NcaType = ncaType,
        FileType = FileType,
        TitleType = TitleType,
        NcaSig = NcaSig,
        NcaDistType = NcaDistType,
        UserMetadata = UserMetadata,
        IdOffset = IdOffset,
        SourceIdOffset = SourceIdOffset,
    };
}