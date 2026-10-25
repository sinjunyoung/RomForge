using N3DS.Core.Models;

namespace N3DS.Core.Interfaces;

public interface IInstallSource : INcsdSource
{
    TmdHeader TmdHeader { get; }

    byte[] TmdRaw { get; }

    ValueTask<(Stream stream, long size)> OpenContentNcchEncrypted(int contentIndex);
}