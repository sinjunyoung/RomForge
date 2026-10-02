using LibHac.Ncm;
using NSW.HacPack.Enums;
using NSW.HacPack.Models;
using static LibHac.Ns.ApplicationControlProperty;

namespace NSW.HacPack.Services;

public class NcaGenerationOptions
{
    public LibHac.Common.Keys.KeySet KeySet = new();
    public ulong TitleId;
    public string TempDirectory = string.Empty;
    public string OutDirectory = string.Empty;
    public string ExefsDirectory = string.Empty;
    public string RomfsDirectory = string.Empty;
    public string LogoDirectory = string.Empty;
    public string NcaDirectory = string.Empty;
    public string BackupDirectory = string.Empty;
    public string ProgramNcaPath = string.Empty;
    public string ControlNcaPath = string.Empty;
    public string LegalNcaPath = string.Empty;
    public string HtmlDocNcaPath = string.Empty;
    public string MetaNcaPath = string.Empty;
    public string DataNcaPath = string.Empty;
    public string PublicDataNcaPath = string.Empty;    
    public string CnmtPath = string.Empty;    
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
    public List<string> ManualNcaPaths = [];
    public Language Language = Language.None;
    public uint SdkVersion;
    public byte IdOffset = 0;
    public byte SourceIdOffset = 0;


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

    public bool IsProgramNcaValid => !string.IsNullOrEmpty(ProgramNcaPath) && File.Exists(ProgramNcaPath);
    public bool IsControlNcaValid => !string.IsNullOrEmpty(ControlNcaPath) && File.Exists(ControlNcaPath);
    public bool IsLegalNcaValid => !string.IsNullOrEmpty(LegalNcaPath) && File.Exists(LegalNcaPath);
    public bool IsHtmlDocNcaValid => !string.IsNullOrEmpty(HtmlDocNcaPath) && File.Exists(HtmlDocNcaPath);
    public bool IsMetaNcaValid => !string.IsNullOrEmpty(MetaNcaPath) && File.Exists(MetaNcaPath);
    public bool IsDataNcaValid => !string.IsNullOrEmpty(DataNcaPath) && File.Exists(DataNcaPath);
    public bool IsPublicDataNcaValid => !string.IsNullOrEmpty(PublicDataNcaPath) && File.Exists(PublicDataNcaPath);

    public GameMetadata? UserMetadata { get; set; }


    public NcaGenerationOptions WithRomfs(string romfsDir, LibHac.FsSystem.NcaHeader.ContentType ncaType)
    => new()
    {
        KeySet = KeySet,
        TitleId = TitleId,
        TempDirectory = TempDirectory,
        OutDirectory = OutDirectory,
        ExefsDirectory = ExefsDirectory,
        RomfsDirectory = romfsDir,
        LogoDirectory = LogoDirectory,
        NcaDirectory = NcaDirectory,
        BackupDirectory = BackupDirectory,
        ProgramNcaPath = ProgramNcaPath,
        ControlNcaPath = ControlNcaPath,
        LegalNcaPath = LegalNcaPath,
        HtmlDocNcaPath = HtmlDocNcaPath,
        MetaNcaPath = MetaNcaPath,
        DataNcaPath = DataNcaPath,
        PublicDataNcaPath = PublicDataNcaPath,
        CnmtPath = CnmtPath,
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
        ManualNcaPaths = [.. ManualNcaPaths],
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