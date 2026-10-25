using System.Buffers;
using System.IO.Pipelines;

namespace N3DS.Core.IO;

public sealed class PipeWriterStream(PipeWriter writer) : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer) => writer.Write(buffer);

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => await WriteAsync(buffer.AsMemory(offset, count), ct);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        var result = await writer.WriteAsync(buffer, ct);

        if (result.IsCanceled) 
            ct.ThrowIfCancellationRequested();

        if (result.IsCompleted) 
            throw new IOException("PipeReader가 완료되었습니다.");
    }

    public override async Task FlushAsync(CancellationToken ct)
    {
        var result = await writer.FlushAsync(ct);

        if (result.IsCanceled) 
            ct.ThrowIfCancellationRequested();
    }

    public override void Flush()
    {
        throw new NotSupportedException("PipeWriterStream에서는 FlushAsync를 사용하세요.");
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { }
}