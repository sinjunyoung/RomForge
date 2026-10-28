using System.Buffers;
using System.IO.Pipelines;

namespace N3DS.Core.IO;

public sealed class PipeReaderStream(PipeReader reader) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValueTask<int> readTask = ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None);

        if (readTask.IsCompleted)
            return readTask.Result;

        return Task.Run(async () => await readTask.ConfigureAwait(false)).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => await ReadAsync(buffer.AsMemory(offset, count), ct);

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (buffer.IsEmpty) 
            return 0;

        while (true)
        {
            var result = await reader.ReadAsync(ct);
            var sequence = result.Buffer;

            if (sequence.IsEmpty && result.IsCompleted)
                return 0;

            if (sequence.IsEmpty)
            {
                reader.AdvanceTo(sequence.Start, sequence.End);
                continue;
            }

            int toCopy = (int)Math.Min(buffer.Length, sequence.Length);

            sequence.Slice(0, toCopy).CopyTo(buffer.Span);

            var consumedPosition = sequence.GetPosition(toCopy);

            reader.AdvanceTo(consumedPosition, consumedPosition);

            return toCopy;
        }
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) { }
}