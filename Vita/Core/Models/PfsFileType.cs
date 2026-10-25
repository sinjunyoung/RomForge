namespace Vita.Core.Models;

public enum PfsFileType : ushort
{
    Unexisting = 0x0000,

    NormalFile = 0x0001,

    NormalDirectory = 0x8000,

    SysDirectory = 0x8006,

    UnencryptedSystemFileRw = 0x4006,

    EncryptedSystemFileRw = 0x0006,

    UnencryptedSystemFileRo = 0x4007,

    EncryptedSystemFileRo = 0x0007,

    AcidDirectory = 0x9004
}