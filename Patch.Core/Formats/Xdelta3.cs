using System.Runtime.InteropServices;
using Common;

namespace Patch.Core.Formats;

public static class Xdelta3
{
    private const string DllName = "xdelta3";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ProgressCallback(double progress);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int xd3_create_patch_w(string sourcePath, string newPath, string patchPath, IntPtr cb);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int xd3_create_patch_w(string sourcePath, string newPath, string patchPath, ProgressCallback cb);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int xd3_apply_patch_mem(byte[] sourceData, nuint sourceSize, byte[] patchData, nuint patchSize, out IntPtr outputData, out nuint outputSize, IntPtr cb);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int xd3_apply_patch_mem(byte[] sourceData, nuint sourceSize, byte[] patchData, nuint patchSize, out IntPtr outputData, out nuint outputSize, ProgressCallback cb);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void xd3_free_mem(IntPtr ptr);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void xd3_cancel();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr xd3_get_last_error();

    private enum Xd3StreamStatus
    {
        Ok = 0,
        NeedInput = 1,
        Finished = 2,
        Error = -1
    }

    [DllImport(DllName, EntryPoint = "xd3_stream_open_decode_buf", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr xd3_stream_open_decode_buf(IntPtr sourceBuf, long sourceSize);

    [DllImport(DllName, EntryPoint = "xd3_stream_feed", CallingConvention = CallingConvention.Cdecl)]
    private static extern int xd3_stream_feed(IntPtr handle, byte[] patchChunk, nuint len, int isLastChunk);

    [DllImport(DllName, EntryPoint = "xd3_stream_read_output", CallingConvention = CallingConvention.Cdecl)]
    private static extern int xd3_stream_read_output(IntPtr handle, byte[] outBuf, nuint outBufCapacity, out nuint written);

    [DllImport(DllName, EntryPoint = "xd3_stream_close", CallingConvention = CallingConvention.Cdecl)]
    private static extern void xd3_stream_close(IntPtr handle);

    private const int StreamChunkSize = 4 * 1024 * 1024;

    private static string GetLastError() => Marshal.PtrToStringAnsi(xd3_get_last_error()) ?? "unknown error";

    public static void ApplyPatch(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        long sourceSize = new FileInfo(sourcePath).Length;
        IntPtr sourceBuf = sourceSize > 0 ? Marshal.AllocHGlobal((nint)sourceSize) : IntPtr.Zero;

        try
        {
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamChunkSize, FileOptions.SequentialScan))
            {
                var chunk = new byte[StreamChunkSize];
                long offset = 0;

                while (offset < sourceSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int n = sourceStream.Read(chunk, 0, (int)Math.Min(chunk.Length, sourceSize - offset));

                    if (n <= 0)
                        throw new InvalidOperationException("소스 파일을 읽는 중 오류가 발생했습니다.");

                    Marshal.Copy(chunk, 0, sourceBuf + (nint)offset, n);
                    offset += n;
                }
            }

            ApplyPatchFromBuffer(sourceBuf, sourceSize, patchPath, outputPath, progress, ct);
        }
        finally
        {
            if (sourceBuf != IntPtr.Zero)
                Marshal.FreeHGlobal(sourceBuf);
        }
    }

    private static void ApplyPatchFromBuffer(IntPtr sourceBuf, long sourceSize, string patchPath, string outputPath, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        IntPtr handle = xd3_stream_open_decode_buf(sourceBuf, sourceSize);

        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"소스 버퍼를 열지 못했습니다: {GetLastError()}");

        try
        {
            using var patchStream = new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamChunkSize, FileOptions.SequentialScan);
            using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, StreamChunkSize);

            long patchTotal = patchStream.Length;
            long patchConsumed = 0;
            long totalWritten = 0;

            Action<long, long>? report = null;

            if (progress is not null)
            {
                var reporter = new ProgressReporter("패치중...", string.Empty, sourceSize, progress);
                report = reporter.CreateAction();
            }

            var readBuf = new byte[StreamChunkSize];
            var outBuf = new byte[StreamChunkSize];

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int n = patchStream.Read(readBuf, 0, readBuf.Length);
                patchConsumed += n;

                bool isLastChunk = patchConsumed >= patchTotal;

                int feedRet = xd3_stream_feed(handle, readBuf, (nuint)n, isLastChunk ? 1 : 0);

                if (feedRet != 0)
                    ThrowIfFailed(feedRet);

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    int status = xd3_stream_read_output(handle, outBuf, (nuint)outBuf.Length, out nuint written);

                    if (status == (int)Xd3StreamStatus.Error)
                        throw new InvalidOperationException($"패치 적용 중 오류: {GetLastError()}");

                    if ((int)written > 0)
                    {
                        outStream.Write(outBuf, 0, (int)written);
                        totalWritten += (int)written;
                        report?.Invoke(totalWritten, sourceSize);
                    }

                    if (status != (int)Xd3StreamStatus.Ok)
                        break;
                }

                if (isLastChunk)
                    break;
            }

            report?.Invoke(Math.Max(totalWritten, sourceSize), sourceSize);
        }
        finally
        {
            xd3_stream_close(handle);
        }
    }

    public static byte[] ApplyPatch(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        int ret;
        IntPtr outPtr;
        nuint outSize;

        using (ct.Register(() => xd3_cancel()))
        {
            if (progress is null)
                ret = xd3_apply_patch_mem(sourceData, (nuint)sourceData.Length, patchData, (nuint)patchData.Length, out outPtr, out outSize, IntPtr.Zero);
            else
            {
                long total = sourceData.Length;
                var reporter = new ProgressReporter("패치중...", string.Empty, total, progress);
                var report = reporter.CreateAction();

                ProgressCallback cb = p =>
                {
                    long current = (long)(p * total);
                    report(current, total);
                };

                GCHandle handle = GCHandle.Alloc(cb);

                try
                {
                    ret = xd3_apply_patch_mem(sourceData, (nuint)sourceData.Length, patchData, (nuint)patchData.Length, out outPtr, out outSize, cb);
                }
                finally
                {
                    handle.Free();
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        ThrowIfFailed(ret);

        try
        {
            var result = new byte[(int)outSize];

            Marshal.Copy(outPtr, result, 0, (int)outSize);

            return result;
        }
        finally
        {
            xd3_free_mem(outPtr);
        }
    }

    public static void CreatePatch(string sourcePath, string newPath, string patchPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, newPath);
        int result;

        using (ct.Register(() => xd3_cancel()))
        {
            if (progress is null)
                result = xd3_create_patch_w(sourcePath, newPath, patchPath, IntPtr.Zero);
            else
            {
                long total = new FileInfo(newPath).Length;
                var reporter = new ProgressReporter("패치 생성중...", string.Empty, total, progress);
                var report = reporter.CreateAction();

                ProgressCallback cb = p =>
                {
                    long current = (long)(p * total);
                    report(current, total);
                };

                GCHandle handle = GCHandle.Alloc(cb);

                try
                {
                    result = xd3_create_patch_w(sourcePath, newPath, patchPath, cb);
                }
                finally
                {
                    handle.Free();
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        ThrowIfFailed(result);
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }

    private static void ThrowIfFailed(int result)
    {
        if (result == 0)
            return;

        int absResult = Math.Abs(result);

        string errorMessage = absResult switch
        {
            17710 => "내부 라이브러리 오류가 발생했습니다. (XD3_INTERNAL)",
            17711 => "잘못된 설정 값입니다. (XD3_INVALID)",
            17712 => "원본 파일이 패치 파일과 일치하지 않습니다. (미스매치 / XD3_INVALID_INPUT)",
            17713 => "보조 압축(Secondary Compression) 효율이 없어 적용할 수 없습니다. (XD3_NOSECOND)",
            17714 => "구현되지 않은 기능이 포함되어 있습니다. (XD3_UNIMPLEMENTED)",

            17703 => "입력 데이터가 더 필요합니다. (XD3_INPUT)",
            17704 => "출력 버퍼가 가득 찼습니다. (XD3_OUTPUT)",
            17705 => "소스 블록 데이터가 더 필요합니다. (XD3_GETSRCBLK)",

            2 => "지정된 파일 또는 경로를 찾을 수 없습니다. (ENOENT)",
            13 => "파일 접근 권한이 없습니다. (EACCES)",
            28 => "디스크 공간이 부족합니다. (ENOSPC)",

            _ => $"{GetLastError()} (Error Code: {result})"
        };

        throw new InvalidOperationException(errorMessage);
    }
}