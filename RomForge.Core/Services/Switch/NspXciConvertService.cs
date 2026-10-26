using Common;
using LibHac.Common.Keys;
using NSW.Core;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public static class NspXciConvertService
{
    public static Task<string> NspToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> XciToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> NspToXczAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> XciToNszAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> NszToXciAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> XczToNspAsync(string inputPath, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> NszToXczAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, outputDir, ct);
    }

    public static Task<string> XczToNszAsync(string inputPath, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir = null,  CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, outputDir, ct);
    }

    private enum ContainerFormat { Nsp, Xci }

    private static Task<string> RunAsync(string inputPath, ContainerFormat outputFormat, bool useCompression, bool validation, bool useBlockMode, int compressionLevel, KeySet keySet, IProgress<ProgressInfo> progress, Action<string, LogLevel, string> log, string? outputDir, CancellationToken ct)
    {
        var output = outputFormat == ContainerFormat.Xci ? OutputContainer.AsXci : OutputContainer.AsNsp;
        var profile = ConvertProfile.ForConversion(inputPath, output, useCompression);
        return ContainerConverter.ConvertAsync(inputPath, output, useCompression, validation, useBlockMode, compressionLevel, profile, keySet, progress, log, outputDir, ct);
    }
}