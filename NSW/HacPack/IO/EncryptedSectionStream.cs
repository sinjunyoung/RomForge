using System.Buffers;
using System.IO;
using NSW.HacPack.Cryptography.Core;
using NSW.HacPack.Models;
using NSW.HacPack.Services;

namespace NSW.HacPack.IO;

public sealed class EncryptedSectionStream : Stream
{
    private const int ParallelWriteThreshold = 1 << 21;
    private const int EncryptChunk = 0x800000;

    private readonly Stream _inner;
    private readonly AesCtrCipher? _cipher;
    private IvfcLevelBuilder? _ivfc;

    private EncryptedSectionStream(Stream inner, AesCtrCipher? cipher)
    {
        _inner = inner;
        _cipher = cipher;
    }

    public static EncryptedSectionStream Create(Stream inner, ref NcaHeader header, int sectionIndex, NcaGenerationOptions settings)
    {
        if (settings.Plaintext != 0)
            return new EncryptedSectionStream(inner, null);

        byte[] key = settings.HasTitleKey == 1 ? settings.TitleKey : settings.KeyAreaKey!;

        return new EncryptedSectionStream(inner, AesCtrCipher.ForSection(key, header.FsHeaders[sectionIndex].SectionCtr));
    }

    public override bool CanRead => false;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public void BeginIvfc() => _ivfc = new IvfcLevelBuilder();

    public void EndIvfc(Stream levelOutput, out ulong generatedSize)
    {
        var builder = _ivfc ?? throw new InvalidOperationException("IVFC 해시 계산이 시작되지 않았습니다.");

        _ivfc = null;

        builder.Complete(levelOutput, out generatedSize);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return;

        var ivfc = _ivfc;

        if (ivfc != null && count >= ParallelWriteThreshold && Environment.ProcessorCount > 1)
        {
            Parallel.Invoke(() => ivfc.Add(buffer, offset, count), () => WriteEncrypted(buffer, offset, count));
            return;
        }

        ivfc?.Add(buffer, offset, count);
        WriteEncrypted(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        byte[] copy = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            buffer.CopyTo(copy);
            Write(copy, 0, buffer.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(copy);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _cipher?.Dispose();

        base.Dispose(disposing);
    }

    private void WriteEncrypted(byte[] buffer, int offset, int count)
    {
        if (_cipher == null)
        {
            _inner.Write(buffer, offset, count);
            return;
        }

        byte[] encrypted = ArrayPool<byte>.Shared.Rent(Math.Min(count, EncryptChunk));

        try
        {
            int done = 0;

            while (done < count)
            {
                int length = Math.Min(EncryptChunk, count - done);

                _cipher.Transform(buffer, offset + done, encrypted, 0, length, (ulong)_inner.Position);
                _inner.Write(encrypted, 0, length);

                done += length;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(encrypted);
        }
    }
}