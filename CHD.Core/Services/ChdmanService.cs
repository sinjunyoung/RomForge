using CHD.Core.Models;
using CHD.Core.Models.Enums;
using Common;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CHD.Core.Services;

public sealed class ChdmanService : IDisposable
{
    private const string CHDMAN_DLL = "chdman.dll";

    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly LogCallback _logCallback;

    private bool _disposed;

    #region DllImport

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ProgressCallback(int percent);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallback([MarshalAs(UnmanagedType.LPStr)] string message);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int chdman_create_cd([MarshalAs(UnmanagedType.LPUTF8Str)] string input, [MarshalAs(UnmanagedType.LPUTF8Str)] string output, ProgressCallback progress, LogCallback log);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int chdman_create_dvd([MarshalAs(UnmanagedType.LPUTF8Str)] string input, [MarshalAs(UnmanagedType.LPUTF8Str)] string output, [MarshalAs(UnmanagedType.LPUTF8Str)] string compression, ProgressCallback progress, LogCallback log);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int chdman_extract_cd([MarshalAs(UnmanagedType.LPUTF8Str)] string input, [MarshalAs(UnmanagedType.LPUTF8Str)] string output, ProgressCallback progress, LogCallback log);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int chdman_extract_raw([MarshalAs(UnmanagedType.LPUTF8Str)] string input, [MarshalAs(UnmanagedType.LPUTF8Str)] string output, ProgressCallback progress, LogCallback log);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern int chdman_get_info([MarshalAs(UnmanagedType.LPUTF8Str)] string input, LogCallback log);

    [DllImport(CHDMAN_DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void chdman_cancel();

    #endregion

    public event EventHandler<string>? ErrorReceived;

    public ChdmanService() => _logCallback = msg => { if (!string.IsNullOrEmpty(msg)) ErrorReceived?.Invoke(this, msg); };

    public Task<bool> CreateCdAsync(string cuePath, string chdPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        cuePath = Path.GetFullPath(cuePath);
        chdPath = Path.GetFullPath(chdPath);

        return RunLockedAsync(Path.GetDirectoryName(cuePath)!, Path.GetFileName(cuePath), chdPath, "압축 중...", chdman_create_cd, progress, ct);
    }

    public Task<bool> CreateDvdAsync(string isoPath, string chdPath, string compression = "zlib", IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        isoPath = Path.GetFullPath(isoPath);
        chdPath = Path.GetFullPath(chdPath);

        return RunLockedAsync(Path.GetDirectoryName(isoPath) ?? Path.GetPathRoot(isoPath)!, isoPath, chdPath, "압축 중...", (i, o, p, l) => chdman_create_dvd(i, o, compression, p, l), progress, ct);
    }

    public Task<bool> ExtractCdAsync(string chdPath, string cuePath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        chdPath = Path.GetFullPath(chdPath);
        cuePath = Path.GetFullPath(cuePath);

        return RunLockedAsync(Path.GetDirectoryName(cuePath)!, chdPath, cuePath, "추출 중...", chdman_extract_cd, progress, ct);
    }

    public Task<bool> ExtractRawAsync(string chdPath, string isoPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        chdPath = Path.GetFullPath(chdPath);
        isoPath = Path.GetFullPath(isoPath);

        return RunLockedAsync(Path.GetDirectoryName(isoPath)!, chdPath, isoPath, "추출 중...", chdman_extract_raw, progress, ct);
    }

    private async Task<bool> RunLockedAsync(string workingDir, string input, string output, string label, Func<string, string, ProgressCallback, LogCallback, int> invoke, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            using var cancelReg = ct.Register(static () => chdman_cancel());

            return await Task.Run(() => RunWithCwd(workingDir, input, output, label, invoke, progress, cancelReg, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool RunWithCwd(string workingDir, string input, string output, string label, Func<string, string, ProgressCallback, LogCallback, int> invoke, IProgress<ProgressInfo>? progress, CancellationTokenRegistration cancelReg, CancellationToken ct)
    {
        string originalDir = Directory.GetCurrentDirectory();
        int lastProgress = 0;

        string ext = Path.GetExtension(input);

        long totallSize = 0;

        switch (ext.ToLowerInvariant())
        {
            case ".cue":
                {
                    var referencedBins = ConversionSource.ParseBinsFromCue(Path.Combine(workingDir, input));                    

                    foreach (var binName in referencedBins)
                    {
                        string binPath = Path.Combine(workingDir, Path.GetFileName(binName));

                        if (File.Exists(binPath))
                            totallSize += new FileInfo(binPath).Length;
                    }
                }
                break;
            case ".gdi":
                {
                    var referencedBins = ConversionSource.ParseFilesFromGdi(Path.Combine(workingDir, input));

                    foreach (var binName in referencedBins)
                    {
                        string binPath = Path.Combine(workingDir, Path.GetFileName(binName));

                        if (File.Exists(binPath))
                            totallSize += new FileInfo(binPath).Length;
                    }
                }
                break;
            case ".bin":
            case ".iso":
            case ".chd":
                {
                    string path = Path.Combine(workingDir, input);

                    if (File.Exists(path))
                        totallSize = new FileInfo(path).Length;
                }
                break;
        }

        var reporter = progress is null ? null : new ProgressReporter(label, string.Empty, totallSize, progress);

        ProgressCallback progressCallback = percent =>
        {
            if (percent >= lastProgress)
            {
                lastProgress = percent;                
                reporter?.ReportPercent(percent / 100.0);
            }
        };

        GCHandle handle = GCHandle.Alloc(progressCallback);

        try
        {
            Directory.SetCurrentDirectory(workingDir);
            int result = invoke(input, output, progressCallback, _logCallback);

            cancelReg.Dispose();

            if (ct.IsCancellationRequested || result == -1)
                throw new OperationCanceledException(ct);

            return result == 0;
        }
        finally
        {
            handle.Free();
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    public static ChdmanInfo GetChdInfo(string chdPath)
    {
        chdPath = Path.GetFullPath(chdPath);

        if (!File.Exists(chdPath))
            throw new FileNotFoundException("CHD 파일을 찾을 수 없습니다.", chdPath);

        try
        {
            var info = ChdInfoReader.ReadChdInfo(chdPath);
            var fileInfo = new FileInfo(chdPath);
            long originalSize = CalculateOriginalSize(info);
            string ratio = originalSize > 0 ? $"{(double)fileInfo.Length / originalSize * 100:F1}%" : "0%";

            return new ChdmanInfo
            {
                FileVersion = $"{info.Version}",
                LogicalSize = originalSize.ToString("N0"),
                ChdSize = fileInfo.Length.ToString("N0"),
                Ratio = ratio,
                Compression = info.GetCompressionInfo()
            };
        }
        catch
        {
            return GetChdInfoViaChdman(chdPath);
        }
    }

    private static ChdmanInfo GetChdInfoViaChdman(string chdPath)
    {
        var sb = new StringBuilder();

        LogCallback logDelegate = msg => sb.AppendLine(msg);

        int result = chdman_get_info(chdPath, logDelegate);

        GC.KeepAlive(logDelegate);

        if (result != 0)
            throw new InvalidOperationException($"chdman info 실패 (code={result})");

        return ParseChdInfo(sb.ToString())
            ?? throw new InvalidOperationException("CHD 정보 파싱 실패");
    }

    public static long CalculateOriginalSize(ChdInfo info)
    {
        if (info.SourceType is ChdSourceType.ISO or ChdSourceType.BinCue)
        {
            if (info.Tracks?.Length > 0)
            {
                return info.Tracks.Sum(track =>
                {
                    string t = track.TrackType?.ToUpperInvariant() ?? string.Empty;

                    int sectorSize = t switch
                    {
                        _ when t.Contains("AUDIO") => 2352,
                        _ when t.Contains("MODE1_RAW") => 2352,
                        _ when t.Contains("MODE1") => 2048,
                        _ when t.Contains("MODE2_RAW") => 2352,
                        _ when t.Contains("MODE2_FORM1") => 2048,
                        _ when t.Contains("MODE2_FORM2") => 2324,
                        _ when t.Contains("MODE2") => 2352,
                        _ => 2048
                    };

                    return (long)track.Frames * sectorSize;
                });
            }

            return (long)info.LogicalBytes;
        }

        return (long)info.LogicalBytes;
    }

    private static ChdmanInfo? ParseChdInfo(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        static string Match(string text, string pattern) =>
            Regex.Match(text, pattern).Groups[1].Value.Trim();

        var version = Match(output, @"File Version:\s*(.+)");
        var logicalSize = Match(output, @"Logical size:\s*([\d,]+)");
        var chdSize = Match(output, @"CHD size:\s*([\d,]+)");
        var ratio = Match(output, @"Ratio:\s*(.+)");

        if (string.IsNullOrEmpty(version) && string.IsNullOrEmpty(logicalSize))
            return null;

        return new ChdmanInfo
        {
            FileVersion = version,
            LogicalSize = logicalSize,
            ChdSize = chdSize,
            Ratio = ratio
        };
    }

    public void Dispose()
    {
        if (_disposed) 
            return;

        _disposed = true;
        _lock.Dispose();
    }
}