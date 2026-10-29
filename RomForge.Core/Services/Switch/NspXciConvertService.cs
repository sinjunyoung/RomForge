using Common;
using LibHac.Common.Keys;
using NSW.Core;
using Res = NSW.Properties.Resources;

namespace RomForge.Core.Services.Switch;

public static class NspXciConvertService
{
    public static Task<string> NspToXciAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNspAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NspToXczAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNszAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXciAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNspAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, false, false, false, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXczAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNszAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, bool useBlockMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Res.Main_Err_NoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, true, validation, useBlockMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    private enum ContainerFormat { Nsp, Xci }

    private static Task<string> RunAsync(string inputPath, string? outputDir, ContainerFormat outputFormat, bool useCompression, bool validation, bool useBlockMode, int compressionLevel, KeySet keySet, 
        IProgress<ProgressInfo>? progress, Action<string, LogLevel, string>? log, CancellationToken ct = default)
    {
        var output = outputFormat == ContainerFormat.Xci ? OutputContainer.AsXci : OutputContainer.AsNsp;
        var profile = ConvertProfile.ForConversion(inputPath, output, useCompression);
        return ContainerConverter.ConvertAsync(inputPath, outputDir, output, useCompression, validation, useBlockMode, compressionLevel, profile, keySet, progress, log, ct);
    }
}