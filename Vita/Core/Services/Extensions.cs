using Vita.Core.Models;

namespace Vita.Core.Services;

public static class Extensions
{
    public static bool IsDirectory(this PfsFileType type) => type is PfsFileType.NormalDirectory or PfsFileType.SysDirectory or PfsFileType.AcidDirectory;

    public static bool IsEncrypted(this PfsFileType type) => type is PfsFileType.EncryptedSystemFileRw or PfsFileType.EncryptedSystemFileRo or PfsFileType.NormalFile;

    public static bool IsUnexisting(this PfsFileType type) => type == PfsFileType.Unexisting;
}