using N3DS.Core.Enums;

namespace N3DS.Core.Models;

public class InstalledTitle
{
    public string TitleId { get; init; } = string.Empty;

    public string TitleIdHigh { get; init; } = string.Empty;

    public string TitleIdLow { get; init; } = string.Empty;

    public TitleType Type { get; init; }

    public ushort Version { get; init; }

    public uint SaveSize { get; init; }

    public Contents[] Contents { get; init; } = [];

    public string TitlePath { get; init; } = string.Empty;

    public string ContentPath { get; init; } = string.Empty;

    public bool HasSave { get; init; }

    public ulong ContentSize { get; set; }

    public bool IsDlc => Type == TitleType.DlcContent;

    public bool IsPatch => Type == TitleType.Patch;

    public bool IsApplication => Type == TitleType.Application || Type == TitleType.SystemApplication;
}