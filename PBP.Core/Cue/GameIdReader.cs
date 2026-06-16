using DiscUtils.Iso9660;
using PBP.Core.Models;
using PBP.Core.Readers;
using System.Text.RegularExpressions;

namespace PBP.Core.Cue;

public static class GameIdReader
{
    private static readonly Regex BootRegex = new(@"BOOT\s*=\s*(.*)", RegexOptions.IgnoreCase);
    private static readonly Regex SerialRegex = new(@"([A-Z]+)_?(\d+)\.(\d+)", RegexOptions.IgnoreCase);

    public static string ReadFromDisk(DiskSource source)
    {
        try
        {
            Stream baseStream;

            if (source.Type == DiskSourceType.Chd)
                baseStream = new ChdWrapperStream(source.FilePath);
            else
                baseStream = new FileStream(source.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            using (baseStream)
            {
                Stream isoStream = baseStream;

                if (source.Type != DiskSourceType.Chd)
                    if (baseStream.Length >= 2352 && (baseStream.Length % 2352) == 0)
                        isoStream = new RawToCookedStream(baseStream);

                using var cdReader = new CDReader(isoStream, false);
                var systemCnf = cdReader.Root.GetFiles()
                    .FirstOrDefault(f => f.Name.StartsWith("SYSTEM.CNF", StringComparison.OrdinalIgnoreCase));

                if (systemCnf == null)
                    return "SLUS00000";

                using var reader = new StreamReader(systemCnf.OpenRead());
                string text = reader.ReadToEnd();
                var bootMatch = BootRegex.Match(text);

                if (!bootMatch.Success)
                    return "SLUS00000";

                var serialMatch = SerialRegex.Match(bootMatch.Groups[1].Value);

                if (!serialMatch.Success)
                    return "SLUS00000";

                return string.Concat(serialMatch.Groups[1].Value, serialMatch.Groups[2].Value, serialMatch.Groups[3].Value).ToUpperInvariant();
            }
        }
        catch(Exception e)
        {
            return "SLUS00000";
        }
    }

    public sealed class ChdWrapperStream : Stream
    {
        private readonly ChdReader _chd;
        private readonly byte[] _sectorBuffer;
        private long _position;

        public ChdWrapperStream(string path)
        {
            _chd = new ChdReader(path);

            var test = _chd.ReadSectors(0, 1);

            if (test.Length == 2048)
            {
                SectorSize = 2048;
                DataOffset = 0;
            }
            else if (test.Length == 2352)
            {
                SectorSize = 2352;
                DataOffset = 24;
            }
            else
            {
                throw new InvalidDataException(
                    $"Unsupported CHD sector size: {test.Length}");
            }

            _sectorBuffer = new byte[SectorSize];
        }

        private int SectorSize { get; }
        private int DataOffset { get; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;

        public override long Length => _chd.TotalSectors * 2048;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;

            while (count > 0 && _position < Length)
            {
                long sector = _position / 2048;
                int sectorOffset = (int)(_position % 2048);
                byte[] raw = _chd.ReadSectors(sector, 1);
                int available = 2048 - sectorOffset;
                int copy = Math.Min(count, available);

                Buffer.BlockCopy(raw, DataOffset + sectorOffset, buffer, offset, copy);

                offset += copy;
                count -= copy;
                total += copy;
                _position += copy;
            }

            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => _position
            };

            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _chd.Dispose();
            base.Dispose(disposing);
        }
    }

    private class RawToCookedStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _length;
        private long _position;

        public RawToCookedStream(Stream baseStream)
        {
            _baseStream = baseStream;
            _length = (baseStream.Length / 2352) * 2048;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                long currentSector = (_position + totalRead) / 2048;
                int offsetInSector = (int)((_position + totalRead) % 2048);

                _baseStream.Position = (currentSector * 2352) + 24 + offsetInSector;

                int canReadInSector = 2048 - offsetInSector;
                int toRead = Math.Min(count - totalRead, canReadInSector);
                int read = _baseStream.Read(buffer, offset + totalRead, toRead);

                if (read == 0) 
                    break;

                totalRead += read;
            }

            _position += totalRead;

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => _position
            };
            _position = Math.Clamp(target, 0, _length);

            return _position;
        }

        public override void Flush() => _baseStream.Flush();

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}