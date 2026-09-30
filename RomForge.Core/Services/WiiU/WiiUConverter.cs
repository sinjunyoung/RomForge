using System.IO;
using WiiU.Core.Services;

namespace RomForge.Core.Services.WiiU;

public static class WiiUConverter
{
    public static IReadOnlyList<ITitleSource> OpenSources(string path, string? keysTxtPath)
    {
        if (Directory.Exists(path))
            return [WupTitleSource.LooksLikeWupFolder(path) ? new WupTitleSource(path) : new FolderTitleSource(path)];

        return UnpackService.OpenAll(path, keysTxtPath);
    }

    public static void ConvertToWup(ITitleSource source, string outputFolder, Action<int, int, string>? onFileProgress, CancellationToken ct)
    {
        var (files, patchZipHandle) = RepackService.BuildFileEntries(source, patchPath: null);
        using var _ = patchZipHandle;
        ulong titleId = Convert.ToUInt64(source.TitleIdHex, 16);

        WupPacker.Pack(outputFolder, titleId, (ushort)source.TitleVersion, files, (done, total, label) => onFileProgress?.Invoke(total > 0 ? (int)(done * 100.0 / total) : 100, 100, label), ct);
    }

    public static void ConvertToLoadiine(ITitleSource source, string outputFolder, Action<int, int, string>? onFileProgress, CancellationToken ct) => source.ExtractTo(outputFolder, ToPercentProgress(onFileProgress), ct);

    public static void ConvertToWua(ITitleSource source, string outputWuaPath, Action<int, int, string>? onFileProgress, CancellationToken ct) 
        => WiiURepackService.Repack(source, outputWuaPath, patchFolder: null, titleIdHexOverride: null, titleVersionOverride: null, ToPercentProgress(onFileProgress), ct: ct);

    private static Action<long, long, string>? ToPercentProgress(Action<int, int, string>? callback)
    {
        if (callback is null)
            return null;

        return (done, total, label) => callback(total > 0 ? (int)(done * 100.0 / total) : 100, 100, label);
    }

    public static string BuildOutputName(ITitleSource source, string? titleName)
    {
        string safeName = NSW.Utils.NspNameBuilder.SafeFileName(titleName ?? source.TitleIdHex);

        return $"{safeName} [{source.TitleIdHex}_v{source.TitleVersion}]";
    }
}