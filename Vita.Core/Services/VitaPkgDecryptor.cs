using System.Buffers.Binary;
using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaPkgDecryptor
{
    private const uint PkgMagic = 0x7f504b47;
    private const uint ExtMagic = 0x7f657874;

    public static VitaPkgHeader ReadHeader(Stream pkgStream)
    {
        pkgStream.Seek(0, SeekOrigin.Begin);

        var header = new byte[VitaPkgHeader.HeaderSize + VitaPkgHeader.HeaderExtSize];

        pkgStream.ReadExactly(header);

        if (BinaryPrimitives.ReadUInt32BigEndian(header) != PkgMagic || BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(VitaPkgHeader.HeaderSize)) != ExtMagic)
            throw new InvalidDataException("PKG 파일이 아닙니다.");

        long metaOffset = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(8));
        int metaCount = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12));
        int itemCount = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20));
        long totalSize = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(24));
        long encOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(32));
        long encSize = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(40));
        byte[] iv = header.AsSpan(0x70, 16).ToArray();
        int keyType = header[0xe7] & 7;
        uint contentType = 0;
        long itemsOffset = 0;
        long itemsSize = 0;
        long cursor = metaOffset;

        for (int i = 0; i < metaCount; i++)
        {
            pkgStream.Seek(cursor, SeekOrigin.Begin);

            var block = new byte[16];

            pkgStream.ReadExactly(block);

            uint type = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(0));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(4));

            if (type == 2)
                contentType = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(8));
            else if (type == 13)
            {
                itemsOffset = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(8));
                itemsSize = BinaryPrimitives.ReadUInt32BigEndian(block.AsSpan(12));
            }

            cursor += 8 + size;
        }

        if (contentType is not ((uint)VitaContentType.App or (uint)VitaContentType.Dlc or (uint)VitaContentType.Psm or (uint)VitaContentType.PsmUnk))
            throw new NotSupportedException($"지원하지 않는 PKG content type: 0x{contentType:x}");

        return new VitaPkgHeader
        {
            MetaOffset = metaOffset,
            MetaCount = metaCount,
            ItemCount = itemCount,
            TotalSize = totalSize,
            EncOffset = encOffset,
            EncSize = encSize,
            Iv = iv,
            KeyType = keyType,
            ContentType = contentType,
            ItemsOffset = itemsOffset,
            ItemsSize = itemsSize
        };
    }

    public static void ExtractTo(Stream pkgStream, VitaPkgHeader header, string outputDir, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        byte[] mainKey = VitaPkgKeys.DeriveMainKey(header.KeyType, header.Iv);
        using var ctr = new VitaAes128Ctr(mainKey, header.Iv);

        Directory.CreateDirectory(outputDir);

        for (int i = 0; i < header.ItemCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            long itemOffset = header.ItemsOffset + i * 32;
            var item = new byte[32];

            pkgStream.Seek(header.EncOffset + itemOffset, SeekOrigin.Begin);
            pkgStream.ReadExactly(item);
            ctr.XorAt(itemOffset / 16, item);

            uint nameOffset = BinaryPrimitives.ReadUInt32BigEndian(item.AsSpan(0));
            uint nameSize = BinaryPrimitives.ReadUInt32BigEndian(item.AsSpan(4));
            long dataOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(item.AsSpan(8));
            long dataSize = (long)BinaryPrimitives.ReadUInt64BigEndian(item.AsSpan(16));
            byte flags = item[27];
            var nameBytes = new byte[nameSize];

            pkgStream.Seek(header.EncOffset + nameOffset, SeekOrigin.Begin);
            pkgStream.ReadExactly(nameBytes);
            ctr.XorAt(nameOffset / 16, nameBytes);

            string name = System.Text.Encoding.UTF8.GetString(nameBytes);
            bool isDirectory = flags is 4 or 18;
            string outPath = Path.Combine(outputDir, name.Replace('/', Path.DirectorySeparatorChar));

            if (isDirectory)
            {
                Directory.CreateDirectory(outPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            using var outFile = File.Create(outPath);
            const int chunkSize = 1 * 1024 * 1024;
            long remaining = dataSize;
            long blockCursor = dataOffset;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();

                int toRead = (int)Math.Min(chunkSize, remaining);
                var buffer = new byte[toRead];

                pkgStream.Seek(header.EncOffset + blockCursor, SeekOrigin.Begin);
                pkgStream.ReadExactly(buffer);
                ctr.XorAt(blockCursor / 16, buffer);
                outFile.Write(buffer);

                blockCursor += toRead;
                remaining -= toRead;
            }

            progress?.Report((double)(i + 1) / header.ItemCount);
        }
    }
}