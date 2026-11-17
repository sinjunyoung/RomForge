using Common.Models;
using Common.Localization;
using Common.Services;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Patch.Core.Formats;

public static partial class Xdelta3
{
    private const string DllName = "xdelta3";

    private enum Xd3StreamStatus
    {
        Ok = 0,

        NeedInput = 1,

        Finished = 2,

        Error = -1
    }

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr xd3_get_last_error();

    [LibraryImport(DllName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int xd3_get_last_error_code();

    [LibraryImport(DllName, EntryPoint = "xd3_stream_open_decode_buf")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr xd3_stream_open_decode_buf(IntPtr sourceBuf, long sourceSize);

    [LibraryImport(DllName, EntryPoint = "xd3_stream_feed")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int xd3_stream_feed(IntPtr handle, [In] byte[] patchChunk, nuint len, int isLastChunk);

    [LibraryImport(DllName, EntryPoint = "xd3_stream_read_output")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int xd3_stream_read_output(IntPtr handle, [Out] byte[] outBuf, nuint outBufCapacity, out nuint written);

    [LibraryImport(DllName, EntryPoint = "xd3_stream_finish")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int xd3_stream_finish(IntPtr handle);

    [LibraryImport(DllName, EntryPoint = "xd3_stream_close")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void xd3_stream_close(IntPtr handle);

    private const int StreamChunkSize = 4 * 1024 * 1024;

    private static string GetLastError() => Marshal.PtrToStringAnsi(xd3_get_last_error()) ?? "unknown error";

    public static Stream OpenDecoder(Func<Stream> openSource, long sourceLength, Func<Stream> openPatch, long patchLength, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var source = SourceBuffer.Load(openSource, sourceLength, progress, ct);

        try
        {
            return new DecodeStream(source, true, openPatch, patchLength, sourceLength, progress, ct);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public static void ApplyPatch(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        long sourceSize = new FileInfo(sourcePath).Length;
        long patchSize = new FileInfo(patchPath).Length;

        using var decoder = OpenDecoder(() => OpenSequential(sourcePath), sourceSize, () => OpenSequential(patchPath), patchSize, progress, ct);
        using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, StreamChunkSize);

        decoder.CopyTo(outStream, StreamChunkSize);
    }

    public static byte[] ApplyPatch(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        if (patchData.Length == 0)
            throw new InvalidOperationException(Loc.S.ErrPatchDataEmpty);

        using var source = SourceBuffer.Pin(sourceData);
        using var decoder = new DecodeStream(source, false, () => new MemoryStream(patchData, false), patchData.Length, sourceData.Length, progress, ct);
        using var outStream = new MemoryStream(sourceData.Length);

        decoder.CopyTo(outStream, StreamChunkSize);

        return outStream.ToArray();
    }

    private static FileStream OpenSequential(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read, StreamChunkSize, FileOptions.SequentialScan);

    private sealed class SourceBuffer : IDisposable
    {
        private GCHandle? _pinned;

        private bool _owned;

        private SourceBuffer(IntPtr pointer, long size)
        {
            Pointer = pointer;
            Size = size;
        }

        public IntPtr Pointer { get; private set; }

        public long Size { get; }

        public static SourceBuffer Load(Func<Stream> openSource, long size, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            IntPtr pointer = size > 0 ? Marshal.AllocHGlobal((nint)size) : IntPtr.Zero;
            var buffer = new SourceBuffer(pointer, size) { _owned = pointer != IntPtr.Zero };

            try
            {
                ProgressReporter? loadReporter = progress is null || size <= 0 ? null : new ProgressReporter(Loc.S.StatusLoadingSource, string.Empty, size, progress);
                Action<long, long>? loadReport = loadReporter?.CreateAction();

                using var sourceStream = openSource();

                var chunk = new byte[StreamChunkSize];
                long offset = 0;

                while (offset < size)
                {
                    ct.ThrowIfCancellationRequested();

                    int n = sourceStream.Read(chunk, 0, (int)Math.Min(chunk.Length, size - offset));

                    if (n <= 0)
                        throw new InvalidOperationException(Loc.S.ErrSourceFileReadError);

                    Marshal.Copy(chunk, 0, pointer + (nint)offset, n);

                    offset += n;

                    loadReport?.Invoke(offset, size);
                }

                return buffer;
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        public static SourceBuffer Pin(byte[] data)
        {
            if (data.Length == 0)
                return new SourceBuffer(IntPtr.Zero, 0);

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            return new SourceBuffer(handle.AddrOfPinnedObject(), data.Length) { _pinned = handle };
        }

        public void Dispose()
        {
            if (_pinned.HasValue)
            {
                _pinned.Value.Free();
                _pinned = null;
            }

            if (_owned && Pointer != IntPtr.Zero)
                Marshal.FreeHGlobal(Pointer);

            _owned = false;
            Pointer = IntPtr.Zero;
        }
    }

    private sealed class DecodeStream : Stream
    {
        private readonly SourceBuffer _source;
        private readonly bool _ownsSource;
        private readonly IntPtr _handle;
        private readonly Stream _patch;
        private readonly long _patchTotal;
        private readonly long _estimatedTotal;
        private readonly Action<long, long>? _report;
        private readonly byte[] _readBuf;
        private readonly byte[] _outBuf;
        private readonly CancellationToken _ct;

        private long _patchConsumed;
        private long _totalWritten;
        private int _outPos;
        private int _outCount;
        private bool _patchFedLast;
        private bool _finished;
        private bool _disposed;

        public DecodeStream(SourceBuffer source, bool ownsSource, Func<Stream> openPatch, long patchLength, long sourceLength, IProgress<ProgressInfo>? progress, CancellationToken ct)
        {
            if (patchLength <= 0)
                throw new InvalidOperationException(Loc.S.ErrPatchFileEmpty);

            _source = source;
            _ownsSource = ownsSource;
            _patchTotal = patchLength;
            _estimatedTotal = Math.Max(sourceLength, 1);
            _ct = ct;
            _handle = xd3_stream_open_decode_buf(source.Pointer, source.Size);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException(Loc.S.ErrSourceBufferOpenFailed(GetLastError()));

            try
            {
                _patch = openPatch();
                _report = progress is null ? null : new ProgressReporter(Loc.S.StatusApplyingPatch, string.Empty, _estimatedTotal, progress).CreateAction();
                _readBuf = GC.AllocateArray<byte>(StreamChunkSize, pinned: true);
                _outBuf = GC.AllocateArray<byte>(StreamChunkSize, pinned: true);
            }
            catch
            {
                xd3_stream_close(_handle);
                _patch?.Dispose();
                throw;
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int total = 0;

            while (total < buffer.Length)
            {
                if (_outPos >= _outCount && !Fill())
                    break;

                int copy = Math.Min(buffer.Length - total, _outCount - _outPos);

                _outBuf.AsSpan(_outPos, copy).CopyTo(buffer[total..]);

                _outPos += copy;
                total += copy;
            }

            return total;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                xd3_stream_close(_handle);
                _patch.Dispose();

                if (_ownsSource)
                    _source.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool Fill()
        {
            while (true)
            {
                _ct.ThrowIfCancellationRequested();

                if (_finished)
                    return false;

                int status = xd3_stream_read_output(_handle, _outBuf, (nuint)_outBuf.Length, out nuint written);

                if (status == (int)Xd3StreamStatus.Error)
                    ThrowLastError();

                if (written > 0)
                {
                    _outPos = 0;
                    _outCount = (int)written;
                    _totalWritten += (int)written;

                    _report?.Invoke(Math.Min(_totalWritten, _estimatedTotal - 1), _estimatedTotal);

                    return true;
                }

                bool drained = status == (int)Xd3StreamStatus.Finished || status == (int)Xd3StreamStatus.NeedInput && _patchFedLast;

                if (drained)
                {
                    FinishStream(_handle);

                    _report?.Invoke(_estimatedTotal, _estimatedTotal);
                    _finished = true;

                    return false;
                }

                if (status == (int)Xd3StreamStatus.NeedInput)
                    FeedNext();
            }
        }

        private void FeedNext()
        {
            int n = _patch.Read(_readBuf, 0, _readBuf.Length);

            if (n <= 0)
            {
                if (xd3_stream_feed(_handle, _readBuf, 0, 1) != 0)
                    ThrowLastError();

                _patchFedLast = true;

                return;
            }

            _patchConsumed += n;

            bool isLastChunk = _patchConsumed >= _patchTotal;

            if (xd3_stream_feed(_handle, _readBuf, (nuint)n, isLastChunk ? 1 : 0) != 0)
                ThrowLastError();

            _patchFedLast = isLastChunk;
        }
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException(Loc.S.ErrFileNotFoundName(path));
    }

    private static void FinishStream(IntPtr handle)
    {
        if (xd3_stream_finish(handle) != 0)
            ThrowLastError();
    }

    private static void ThrowLastError() => throw new InvalidOperationException(Loc.S.ErrPatchApplyError(DescribeError(xd3_get_last_error_code())));

    private static string DescribeError(int code)
    {
        int absCode = Math.Abs(code);

        return absCode switch
        {
            17710 => Loc.S.ErrXd3Internal(GetLastError()),
            17711 => Loc.S.ErrXd3Invalid,
            17712 => Loc.S.ErrXd3InvalidInput,
            17713 => Loc.S.ErrXd3NoSecond,
            17714 => Loc.S.ErrXd3Unimplemented,

            17703 => Loc.S.ErrXd3Input,
            17704 => Loc.S.ErrXd3Output,
            17705 => Loc.S.ErrXd3GetSrcBlk,

            2 => Loc.S.ErrErrnoEnoent,
            13 => Loc.S.ErrErrnoEacces,
            28 => Loc.S.ErrErrnoEnospc,

            _ => GetLastError()
        };
    }
}