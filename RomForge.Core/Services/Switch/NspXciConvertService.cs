using Common.Models;
using Common.Localization;
using LibHac.Common.Keys;
using NSW.Core;

namespace RomForge.Core.Services.Switch;

public static class NspXciConvertService
{
    private enum ContainerFormat { Nsp, Xci }

    public static Task<string> NspToXciAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, false, false, SwitchCompressionMode.None, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNspAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, false, false, SwitchCompressionMode.None, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NspToXczAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, true, validation, compressionMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XciToNszAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, true, validation, compressionMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXciAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, false, false, SwitchCompressionMode.None, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNspAsync(string inputPath, string? outputDir, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, false, false, SwitchCompressionMode.None, 0, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> NszToXczAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Xci, true, validation, compressionMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    public static Task<string> XczToNszAsync(string inputPath, string? outputDir, int compressionLevel, bool validation, SwitchCompressionMode compressionMode, IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var keySet = KeySetProvider.Instance.KeySet ?? throw new InvalidOperationException(Loc.S.MainErrNoKeys);
        return RunAsync(inputPath, outputDir, ContainerFormat.Nsp, true, validation, compressionMode, compressionLevel, keySet.Clone(), progress, log, ct);
    }

    private static Task<string> RunAsync(string inputPath, string? outputDir, ContainerFormat outputFormat, bool useCompression, bool validation, SwitchCompressionMode compressionMode, int compressionLevel, KeySet keySet, 
        IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, CancellationToken ct = default)
    {
        var output = outputFormat == ContainerFormat.Xci ? OutputContainer.AsXci : OutputContainer.AsNsp;
        var profile = ConvertProfile.ForConversion(inputPath, output, useCompression);
        return ContainerConverter.ConvertAsync(inputPath, outputDir, output, useCompression, validation, compressionMode, compressionLevel, profile, keySet, progress, log, ct);
    }
}