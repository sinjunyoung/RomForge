namespace WiiGC.Core.Services.GameCube;

public static class RvzToGczConverter
{
    public static void Convert(string inputPath, string outputPath, int blockSize = GczWriter.DefaultBlockSize, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var reader = new RvzDiscReader(inputPath);
            using var output = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None);
            uint subType = DiscHeader.GczSubType(DiscHeader.FromRvzType(reader.DiscType));
            using var writer = new GczWriter(output, reader.IsoSize, blockSize, subType);
            var sink = new GczIsoSink(writer, reader.IsoSize);

            reader.WriteIso(sink, progress, ct);
            sink.Finish();

            succeeded = true;
        }
        finally
        {
            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }
    }

    private sealed class GczIsoSink(GczWriter writer, long totalLength) : IIsoSink
    {
        private long _position;

        public void SetLength(long length)
        {
        }

        public void Write(long offset, ReadOnlySpan<byte> data)
        {
            if (offset < _position)
                throw new InvalidOperationException("GCZ 변환 중 데이터 순서가 올바르지 않습니다.");

            if (offset > _position)
                writer.AppendZeros(offset - _position);

            writer.Append(data);

            _position = offset + data.Length;
        }

        public void Finish()
        {
            if (_position < totalLength)
                writer.AppendZeros(totalLength - _position);

            writer.Finish();
        }
    }
}