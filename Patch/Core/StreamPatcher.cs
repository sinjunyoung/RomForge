using Common.Models;
using Patch.Core.Enums;
using Patch.Core.Formats;
using Patch.Core.Models;

namespace Patch.Core;

public static class StreamPatcher
{
    public static long InMemoryLimit { get; set; } = 128L * 1024 * 1024;

    public static async Task<PatchedContent> PrepareAsync(Func<Stream> openSource, long sourceLength, Func<Stream> openPatch, long patchLength, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        long inMemoryLimit = InMemoryLimit;

        var format = DetectFormat(openPatch);

        return format switch
        {
            PatchFormat.Xdelta => await PrepareXdeltaAsync(openSource, sourceLength, openPatch, patchLength, progress, inMemoryLimit, ct),
            PatchFormat.Unknown => throw new NotSupportedException("지원되지 않거나 유효하지 않은 패치 포맷입니다."),
            _ => await PrepareBufferedAsync(openSource, sourceLength, openPatch, patchLength, progress, inMemoryLimit, ct)
        };
    }

    private static PatchFormat DetectFormat(Func<Stream> openPatch)
    {
        using var stream = openPatch();
        byte[] header = new byte[8];
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return UniversalPatcher.DetectFormat(header.AsSpan(0, read));
    }

    private static async Task<PatchedContent> PrepareXdeltaAsync(Func<Stream> openSource, long sourceLength, Func<Stream> openPatch, long patchLength, IProgress<ProgressInfo>? progress, long inMemoryLimit, CancellationToken ct)
    {
        var capture = new CaptureStream(inMemoryLimit);

        await Task.Run(() =>
        {
            using var decoder = Xdelta3.OpenDecoder(openSource, sourceLength, openPatch, patchLength, progress, ct);

            decoder.CopyTo(capture, 1 << 20);
        }, ct);

        byte[]? bytes = capture.Bytes;

        if (bytes != null)
            return new PatchedContent(bytes.Length, () => new MemoryStream(bytes, false));

        return new PatchedContent(capture.Length, () => Xdelta3.OpenDecoder(openSource, sourceLength, openPatch, patchLength));
    }

    private static async Task<PatchedContent> PrepareBufferedAsync(Func<Stream> openSource, long sourceLength, Func<Stream> openPatch, long patchLength, IProgress<ProgressInfo>? progress, long inMemoryLimit, CancellationToken ct)
    {
        byte[] result = await ComputeAsync(openSource, sourceLength, openPatch, patchLength, progress, ct);

        if (result.Length <= inMemoryLimit)
            return new PatchedContent(result.Length, () => new MemoryStream(result, false));

        long length = result.Length;

        result = [];

        return new PatchedContent(length, () => new DeferredBytesStream(() => ComputeAsync(openSource, sourceLength, openPatch, patchLength, null, CancellationToken.None).GetAwaiter().GetResult()));
    }

    private static async Task<byte[]> ComputeAsync(Func<Stream> openSource, long sourceLength, Func<Stream> openPatch, long patchLength, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        byte[] source = ReadAll(openSource, sourceLength, ct);
        byte[] patch = ReadAll(openPatch, patchLength, ct);

        return await UniversalPatcher.ApplyPatchAsync(source, patch, progress, ct);
    }

    private static byte[] ReadAll(Func<Stream> open, long length, CancellationToken ct)
    {
        if (length > Array.MaxLength)
            throw new NotSupportedException("이 패치 포맷은 2GB를 넘는 파일을 스트리밍으로 처리할 수 없습니다.");

        var data = new byte[length];
        using var stream = open();
        int offset = 0;

        while (offset < data.Length)
        {
            ct.ThrowIfCancellationRequested();

            int read = stream.Read(data, offset, data.Length - offset);

            if (read <= 0)
                throw new InvalidDataException("파일을 읽는 중 예상보다 일찍 끝났습니다.");

            offset += read;
        }

        return data;
    }

    private sealed class CaptureStream(long limit) : Stream
    {
        private MemoryStream? _memory = new();

        private long _length;

        public byte[]? Bytes => _memory?.ToArray();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _length += buffer.Length;

            if (_memory == null)
                return;

            if (_length > limit)
            {
                _memory = null;
                return;
            }

            _memory.Write(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class DeferredBytesStream(Func<byte[]> compute) : Stream
    {
        private byte[]? _data;
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            _data ??= compute();

            int count = (int)Math.Min(buffer.Length, _data.Length - _position);

            if (count <= 0)
                return 0;

            _data.AsSpan((int)_position, count).CopyTo(buffer);

            _position += count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _data = null;

            base.Dispose(disposing);
        }
    }
}