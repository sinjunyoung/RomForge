using N3DS.Core.Crypto;
using N3DS.Core.Models;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace N3DS.Core.IO;

public class NcchDecryptionStream : Stream
{
    private const int BlockSize = 0x200;
    private readonly Stream _baseStream;
    private readonly long _ncchOffset;
    private readonly NcchHeader _header;
    private long _position;

    private readonly byte[] _primaryKey;
    private readonly byte[] _secondaryKey;
    private readonly byte[] _exheaderCtr;
    private readonly byte[] _exefsCtr;
    private readonly byte[] _romfsCtr;

    private readonly ExeFsSection[] _exeFsSections = new ExeFsSection[8];
    private int _exeFsSectionCount;
    private readonly bool _isActuallyEncrypted;
    private readonly bool _leaveOpen;

    public NcchDecryptionStream(Stream baseStream, long ncchOffset, KeyStore keyStore, bool leaveOpen = false)
    {
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _ncchOffset = ncchOffset;
        _leaveOpen = leaveOpen;

        byte[] headerData = new byte[0x200];
        long savedPos = _baseStream.Position;

        _baseStream.Position = _ncchOffset;

        _baseStream.ReadExactly(headerData, 0, 0x200);

        _baseStream.Position = savedPos;

        _header = NcchHeader.Parse(headerData, 0);

        if (_header.Magic != 0x4843434E)
            throw new InvalidDataException("NCCH 매직 번호가 일치하지 않습니다.");

        DeriveKeys(keyStore, out _primaryKey, out _secondaryKey);
        ComputeCtrs(out _exheaderCtr, out _exefsCtr, out _romfsCtr);

        _isActuallyEncrypted = VerifyEncryption();

        if (!_header.NoCrypto && _isActuallyEncrypted && _header.ExefsOffset != 0)
            ParseExeFsSections();
    }

    private bool VerifyEncryption()
    {
        if (_header.NoCrypto)
            return false;

        if (_header.ExtendedHeaderSize == 0)
            return true;

        byte[] exheader = new byte[0x400];
        long savedPos = _baseStream.Position;

        _baseStream.Position = _ncchOffset + 0x200;

        _baseStream.ReadExactly(exheader, 0, 0x400);

        _baseStream.Position = savedPos;

        byte[] tempCtr = (byte[])_exheaderCtr.Clone();

        AesCtrProcessInPlace(exheader, 0, 0x400, _primaryKey, tempCtr, 0);

        byte[] hash = SHA256.HashData(exheader.AsSpan(0, 0x400));

        return hash.SequenceEqual(_header.ExtendedHeaderHash);
    }

    private void ParseExeFsSections()
    {
        long exefsStart = (long)_header.ExefsOffset * BlockSize;
        byte[] exefsHeader = new byte[0x200];
        long savedPos = _baseStream.Position;

        _baseStream.Position = _ncchOffset + exefsStart;

        _baseStream.ReadExactly(exefsHeader, 0, 0x200);

        _baseStream.Position = savedPos;

        byte[] tempCtr = (byte[])_exefsCtr.Clone();

        AesCtrProcessInPlace(exefsHeader, 0, 0x200, _primaryKey, tempCtr, 0);

        for (int i = 0; i < 8; i++)
        {
            int entryOffset = i * 16;
            string name = Encoding.ASCII.GetString(exefsHeader, entryOffset, 8).TrimEnd('\0');
            uint sectionOffset = BitConverter.ToUInt32(exefsHeader, entryOffset + 8);
            uint sectionSize = BitConverter.ToUInt32(exefsHeader, entryOffset + 12);

            if (sectionSize == 0)
                continue;

            bool usePrimaryKey = (name == "icon" || name == "banner");
            long sectionStartInNcch = exefsStart + 0x200 + sectionOffset;

            _exeFsSections[_exeFsSectionCount++] = new ExeFsSection
            {
                StartOffset = sectionStartInNcch,
                EndOffset = sectionStartInNcch + sectionSize,
                Key = usePrimaryKey ? _primaryKey : _secondaryKey,
                CtrOffset = 0x200 + sectionOffset
            };
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long currentPos = _position;
        int bytesRead = 0;

        lock (_baseStream)
        {
            _baseStream.Position = _ncchOffset + currentPos;
            bytesRead = _baseStream.Read(buffer, offset, count);
        }

        if (bytesRead <= 0)
            return bytesRead;

        DecryptBufferRange(buffer, offset, currentPos, bytesRead);

        _position += bytesRead;

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        long currentPos = _position;
        int bytesRead;

        if (_baseStream is FileStream fs)
            bytesRead = await RandomAccess.ReadAsync(fs.SafeFileHandle, buffer.AsMemory(offset, count), _ncchOffset + currentPos, ct);
        else
        {
            lock (_baseStream) { _baseStream.Position = _ncchOffset + currentPos; }

            bytesRead = await _baseStream.ReadAsync(buffer.AsMemory(offset, count), ct);
        }

        if (bytesRead <= 0)
            return bytesRead;

        DecryptBufferRange(buffer, offset, currentPos, bytesRead);

        _position += bytesRead;

        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        byte[] array = new byte[buffer.Length];
        int bytesRead = await ReadAsync(array, 0, array.Length, ct).ConfigureAwait(false);

        array.AsMemory(0, bytesRead).CopyTo(buffer);

        return bytesRead;
    }

    private void DecryptBufferRange(byte[] buffer, int offset, long ncchRelativePos, int length)
    {
        if (!_header.NoCrypto && _isActuallyEncrypted)
        {
            if (_header.ExtendedHeaderSize > 0)
                DecryptSectionIfOverlap(buffer, offset, ncchRelativePos, length, 0x200, 0xA00, _primaryKey, _exheaderCtr, 0);

            if (_header.ExefsOffset != 0)
            {
                long exefsStart = (long)_header.ExefsOffset * BlockSize;
                long exefsHeaderEnd = exefsStart + 0x200;

                DecryptSectionIfOverlap(buffer, offset, ncchRelativePos, length, exefsStart, exefsHeaderEnd, _primaryKey, _exefsCtr, 0);

                for (int i = 0; i < _exeFsSectionCount; i++)
                {
                    var section = _exeFsSections[i];

                    DecryptSectionIfOverlap(buffer, offset, ncchRelativePos, length, section.StartOffset, section.EndOffset, section.Key, _exefsCtr, section.CtrOffset);
                }
            }

            if (_header.RomfsOffset != 0)
            {
                long romfsStart = (long)_header.RomfsOffset * BlockSize;
                long romfsEnd = romfsStart + ((long)_header.RomfsSize * BlockSize);

                DecryptSectionIfOverlap(buffer, offset, ncchRelativePos, length, romfsStart, romfsEnd, _secondaryKey, _romfsCtr, 0);
            }
        }

        long cryptoFlagPos = 0x188 + 7;

        if (ncchRelativePos <= cryptoFlagPos && cryptoFlagPos < ncchRelativePos + length)
        {
            int flagOffset = (int)(cryptoFlagPos - ncchRelativePos);

            buffer[offset + flagOffset] |= 0x04;
        }
    }

    private static void DecryptSectionIfOverlap(byte[] buffer, int offset, long streamPos, int length, long sectionStart, long sectionEnd, byte[] key, byte[] initialCtr, uint ctrBaseOffset)
    {
        long overlapStart = Math.Max(streamPos, sectionStart);
        long overlapEnd = Math.Min(streamPos + length, sectionEnd);

        if (overlapStart >= overlapEnd)
            return;

        int localOffset = (int)(overlapStart - streamPos);
        int localLength = (int)(overlapEnd - overlapStart);
        byte[] ctr = (byte[])initialCtr.Clone();
        long totalByteOffset = (overlapStart - sectionStart) + ctrBaseOffset;

        FastSeekCtr(ctr, totalByteOffset);
        AesCtrProcessInPlace(buffer, offset + localOffset, localLength, key, ctr, (int)(totalByteOffset % 16));
    }

    private static void FastSeekCtr(byte[] ctr, long byteOffset)
    {
        long blocks = byteOffset / 16;

        if (blocks == 0)
            return;

        ulong high = BitConverter.ToUInt64(ctr, 0);
        ulong low = BitConverter.ToUInt64(ctr, 8);

        if (BitConverter.IsLittleEndian)
        {
            high = BinaryPrimitives.ReverseEndianness(high);
            low = BinaryPrimitives.ReverseEndianness(low);
        }

        ulong oldLow = low;

        low += (ulong)blocks;

        if (low < oldLow) 
            high++;

        if (BitConverter.IsLittleEndian)
        {
            high = BinaryPrimitives.ReverseEndianness(high);
            low = BinaryPrimitives.ReverseEndianness(low);
        }

        Buffer.BlockCopy(BitConverter.GetBytes(high), 0, ctr, 0, 8);
        Buffer.BlockCopy(BitConverter.GetBytes(low), 0, ctr, 8, 8);
    }

    private static void AesCtrProcessInPlace(byte[] data, int offset, int size, byte[] key, byte[] initialCtr, int blockOffset)
    {
        if (size <= 0)
            return;

        using var aes = Aes.Create();

        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;

        using var encryptor = aes.CreateEncryptor();
        byte[] ctr = (byte[])initialCtr.Clone();
        byte[] keystream = new byte[16];
        byte[] ctrBuffer = new byte[16];
        int bytesProcessed = 0;

        while (bytesProcessed < size)
        {
            Buffer.BlockCopy(ctr, 0, ctrBuffer, 0, 16);
            encryptor.TransformBlock(ctrBuffer, 0, 16, keystream, 0);

            int chunk = Math.Min(16 - blockOffset, size - bytesProcessed);

            for (int i = 0; i < chunk; i++)
                data[offset + bytesProcessed + i] ^= keystream[blockOffset + i];

            if (blockOffset + chunk >= 16)
                IncrementCtr(ctr);

            bytesProcessed += chunk;
            blockOffset = 0;
        }
    }

    private static void IncrementCtr(byte[] ctr)
    {
        for (int i = 15; i >= 0; i--)
            if (++ctr[i] != 0)
                break;
    }

    private void DeriveKeys(KeyStore keyStore, out byte[] primaryKey, out byte[] secondaryKey)
    {
        if (_header.FixedKey)
        {
            primaryKey = new byte[16];
            secondaryKey = new byte[16];
            return;
        }

        byte[] keyYPrimary = new byte[16];

        Array.Copy(_header.Signature, 0, keyYPrimary, 0, 16);

        primaryKey = keyStore.DeriveNormalKey(0x2C, keyYPrimary);

        int secondarySlot = _header.SecondaryKeySlot switch
        {
            0x00 => 0x2C,
            0x01 => 0x25,
            0x0A => 0x18,
            0x0B => 0x1B,
            _ => 0x2C
        };

        byte[] secondaryKeyY = keyYPrimary;

        if (_header.SeedCrypto)
        {
            byte[] seed = keyStore.GetSeed(_header.ProgramId);

            secondaryKeyY = CalculateSeedKey(keyYPrimary, seed);
        }

        secondaryKey = keyStore.DeriveNormalKey(secondarySlot, secondaryKeyY);
    }

    private static byte[] CalculateSeedKey(byte[] originalKey, byte[] seed)
    {
        byte[] buffer = new byte[32];

        Array.Copy(originalKey, 0, buffer, 0, 16);
        Array.Copy(seed, 0, buffer, 16, 16);

        byte[] hash = SHA256.HashData(buffer);
        byte[] finalKey = new byte[16];

        Array.Copy(hash, 0, finalKey, 0, 16);

        return finalKey;
    }

    private void ComputeCtrs(out byte[] exheaderCtr, out byte[] exefsCtr, out byte[] romfsCtr)
    {
        byte[] partitionId = BitConverter.GetBytes(_header.PartitionId);

        exheaderCtr = new byte[16];
        exefsCtr = new byte[16];
        romfsCtr = new byte[16];

        if (_header.Version == 0 || _header.Version == 2)
        {
            for (int i = 0; i < 8; i++)
                exheaderCtr[i] = partitionId[7 - i];

            Array.Copy(exheaderCtr, exefsCtr, 16);
            Array.Copy(exheaderCtr, romfsCtr, 16);

            exheaderCtr[8] = 1;
            exefsCtr[8] = 2;
            romfsCtr[8] = 3;
        }
        else
        {
            Array.Copy(partitionId, exheaderCtr, 8);
            Array.Copy(exheaderCtr, exefsCtr, 16);
            Array.Copy(exheaderCtr, romfsCtr, 16);
            SetBigEndian32(exheaderCtr, 12, 0x200);
            SetBigEndian32(exefsCtr, 12, (long)_header.ExefsOffset * BlockSize);
            SetBigEndian32(romfsCtr, 12, (long)_header.RomfsOffset * BlockSize);
        }
    }

    private static void SetBigEndian32(byte[] buf, int offset, long value) => BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(offset, 4), (uint)value);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _baseStream.Dispose();

        base.Dispose(disposing);
    }

    public override long Position { get => _position; set => _position = value; }
    public override long Length => _baseStream.Length - _ncchOffset;
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override void Flush() { }
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
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}