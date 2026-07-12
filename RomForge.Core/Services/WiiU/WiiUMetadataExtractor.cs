using RomForge.Core.Models.WiiU;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WiiU.Core.Services;
using ZstdSharp;

namespace RomForge.Core.Services.WiiU
{
    public class WiiUMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();

        #region WUA Structures

        private record struct WuaSectionInfo(
            ulong Offset,
            ulong Size
            );

        private record struct ZArchiveFooter(
            uint Magic,
            uint Version,
            ulong TotalSize,
            WuaSectionInfo SectionCompressedData,
            WuaSectionInfo SectionOffsetRecords,
            WuaSectionInfo SectionNames,
            WuaSectionInfo SectionFileTree,
            WuaSectionInfo SectionMetaDirectory,
            WuaSectionInfo SectionMetaData
        );

        private record struct FileEntry(
            uint NameOffset,
            bool IsFile,
            ulong OffsetOrNodeStart,
            ulong SizeOrCount
        );

        #endregion

        public static async Task<ExtractorMetadata> Extract(string filePath, string? keysTxtPath = null)
        {
            if (_cache.TryGetValue(filePath, out var cached)) 
                return cached;

            var ext = Path.GetExtension(filePath).ToLower();
            var metadata = ext switch
            {
                ".wua" => await ExtractFromWUA(filePath),
                ".wud" or ".wux" => await ExtractFromWud(filePath, keysTxtPath),
                _ => null,
            };

            if (metadata != null) _cache[filePath] = metadata;

            return metadata!;
        }

        public static ExtractorMetadata? ExtractFromTitleSource(ITitleSource source)
        {
            try
            {
                var metadata = new ExtractorMetadata();

                try
                {
                    using var metaXmlStream = source.OpenRead("meta/meta.xml");
                    using var ms = new MemoryStream();
                    metaXmlStream.CopyTo(ms);
                    ParseMetaXml(Encoding.UTF8.GetString(ms.ToArray()), metadata);
                }
                catch (FileNotFoundException) { }

                foreach (var iconPath in new[] { "meta/iconTex.tga", "meta/bootTvTex.tga" })
                {
                    byte[] tgaData;
                    try
                    {
                        using var iconStream = source.OpenRead(iconPath);
                        using var ms = new MemoryStream();
                        iconStream.CopyTo(ms);
                        tgaData = ms.ToArray();
                    }
                    catch (FileNotFoundException) { continue; }

                    var converted = ConvertWiiUIcon(tgaData);
                    if (converted != null) { metadata.Image = converted; break; }
                }

                return metadata;
            }
            catch { return null; }
        }

        public static ExtractorMetadata? ExtractFromFolder(string folderPath)
        {
            string cacheKey = "folder:" + folderPath;

            if (_cache.TryGetValue(cacheKey, out var cached)) 
                return cached;

            try
            {
                var metadata = new ExtractorMetadata();
                string metaXmlPath = Path.Combine(folderPath, "meta", "meta.xml");

                if (File.Exists(metaXmlPath))
                {
                    var xmlText = File.ReadAllText(metaXmlPath);
                    ParseMetaXml(xmlText, metadata);
                }

                string[] iconNames = ["iconTex.tga", "bootTvTex.tga"];

                foreach (var name in iconNames)
                {
                    string iconPath = Path.Combine(folderPath, "meta", name);

                    if (!File.Exists(iconPath))
                        continue;

                    var tgaData = File.ReadAllBytes(iconPath);
                    var converted = ConvertWiiUIcon(tgaData);

                    if (converted != null)
                    {
                        metadata.Image = converted;
                        break;
                    }
                }

                _cache[cacheKey] = metadata;

                return metadata;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<ExtractorMetadata?> ExtractFromWUA(string wuaPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(wuaPath);
                    long fileSize = stream.Length;

                    if (fileSize <= 0x90) 
                        return null;

                    stream.Seek(fileSize - 0x90, SeekOrigin.Begin);

                    byte[] footerData = new byte[0x90];

                    if (stream.Read(footerData, 0, 0x90) != 0x90) 
                        return null;

                    var footer = ParseZArchiveFooter(footerData);

                    if (footer == null) 
                        return null;

                    var nameTable = ReadSection(stream, footer.Value.SectionNames);

                    if (nameTable == null)
                        return null;

                    var fileTree = ReadFileTree(stream, footer.Value.SectionFileTree);

                    if (fileTree == null || fileTree.Length <= 1) 
                        return null;

                    var metadata = new ExtractorMetadata();
                    string? gameDirName = null;

                    if (!fileTree[1].IsFile) gameDirName = GetName(nameTable, fileTree[1].NameOffset);

                    if (gameDirName == null) 
                        return null;

                    var metaXmlNode = FindFile(fileTree, nameTable, $"{gameDirName}/meta/meta.xml");

                    if (metaXmlNode.HasValue)
                    {
                        var metaXmlContent = ReadFileFromWua(stream, fileTree[metaXmlNode.Value], footer.Value);

                        if (metaXmlContent != null)
                        {
                            var xmlText = Encoding.UTF8.GetString(metaXmlContent);

                            ParseMetaXml(xmlText, metadata);
                        }
                    }

                    if (string.IsNullOrEmpty(metadata.Title)) metadata.Title = gameDirName;

                    string[] iconPaths = [$"{gameDirName}/meta/iconTex.tga", $"{gameDirName}/meta/bootTvTex.tga", ];

                    foreach (var iconPath in iconPaths)
                    {
                        var iconNode = FindFile(fileTree, nameTable, iconPath);

                        if (iconNode.HasValue)
                        {
                            var iconData = ReadFileFromWua(stream, fileTree[iconNode.Value], footer.Value);

                            if (iconData != null)
                            {
                                var convertedIcon = ConvertWiiUIcon(iconData);

                                if (convertedIcon != null)
                                {
                                    metadata.Image = convertedIcon;
                                    break;
                                }
                            }
                        }
                    }

                    return metadata;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata?> ExtractFromWud(string path, string? keysTxtPath)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(keysTxtPath) || !File.Exists(keysTxtPath))
                    return null;

                WudReader? wud = null;

                try
                {
                    wud = WudReader.Open(path);

                    var keys = KeyProvider.LoadFromFile(keysTxtPath);
                    byte[]? discKey = WuDiscReader.FindDiscKey(wud, path, keys.KeyCandidates);

                    if (discKey == null)
                        return null;

                    var disc = WuDiscReader.Open(wud, discKey);
                    var metadata = new ExtractorMetadata();

                    var metaXmlBytes = disc.ExtractFile(disc.GmVolume, "meta/meta.xml");

                    if (metaXmlBytes != null)
                    {
                        var xmlText = Encoding.UTF8.GetString(metaXmlBytes);
                        ParseMetaXml(xmlText, metadata);
                    }

                    string[] iconPaths = ["meta/iconTex.tga", "meta/bootTvTex.tga"];

                    foreach (var iconPath in iconPaths)
                    {
                        var iconData = disc.ExtractFile(disc.GmVolume, iconPath);

                        if (iconData == null)
                            continue;

                        var converted = ConvertWiiUIcon(iconData);

                        if (converted != null)
                        {
                            metadata.Image = converted;
                            break;
                        }
                    }

                    return metadata;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    wud?.Dispose();
                }
            });
        }

        #region Meta XML Parsing

        private static void ParseMetaXml(string xmlContent, ExtractorMetadata metadata)
        {
            try
            {
                var longnameMatch = Regex.Match(xmlContent, @"<longname_ko[^>]*>(.+?)</longname_ko>", RegexOptions.Singleline);

                if (!longnameMatch.Success || string.IsNullOrWhiteSpace(longnameMatch.Groups[1].Value))
                    longnameMatch = Regex.Match(xmlContent, @"<longname_en[^>]*>(.+?)</longname_en>", RegexOptions.Singleline);
                if (!longnameMatch.Success || string.IsNullOrWhiteSpace(longnameMatch.Groups[1].Value))
                    longnameMatch = Regex.Match(xmlContent, @"<longname_ja[^>]*>(.+?)</longname_ja>", RegexOptions.Singleline);
                if (!longnameMatch.Success || string.IsNullOrWhiteSpace(longnameMatch.Groups[1].Value))
                    longnameMatch = Regex.Match(xmlContent, @"<longname[^>]*>(.+?)</longname>", RegexOptions.Singleline);

                var shortnameMatch = Regex.Match(xmlContent, @"<shortname_ko[^>]*>(.+?)</shortname_ko>", RegexOptions.Singleline);

                if (!shortnameMatch.Success || string.IsNullOrWhiteSpace(shortnameMatch.Groups[1].Value))
                    shortnameMatch = Regex.Match(xmlContent, @"<shortname_en[^>]*>(.+?)</shortname_en>", RegexOptions.Singleline);
                if (!shortnameMatch.Success || string.IsNullOrWhiteSpace(shortnameMatch.Groups[1].Value))
                    shortnameMatch = Regex.Match(xmlContent, @"<shortname_ja[^>]*>(.+?)</shortname_ja>", RegexOptions.Singleline);
                if (!shortnameMatch.Success || string.IsNullOrWhiteSpace(shortnameMatch.Groups[1].Value))
                    shortnameMatch = Regex.Match(xmlContent, @"<shortname[^>]*>(.+?)</shortname>", RegexOptions.Singleline);

                var publisherMatch = Regex.Match(xmlContent, @"<publisher_ko[^>]*>(.+?)</publisher_ko>", RegexOptions.Singleline);

                if (!publisherMatch.Success || string.IsNullOrWhiteSpace(publisherMatch.Groups[1].Value))
                    publisherMatch = Regex.Match(xmlContent, @"<publisher_en[^>]*>(.+?)</publisher_en>", RegexOptions.Singleline);
                if (!publisherMatch.Success || string.IsNullOrWhiteSpace(publisherMatch.Groups[1].Value))
                    publisherMatch = Regex.Match(xmlContent, @"<publisher_ja[^>]*>(.+?)</publisher_ja>", RegexOptions.Singleline);
                if (!publisherMatch.Success || string.IsNullOrWhiteSpace(publisherMatch.Groups[1].Value))
                    publisherMatch = Regex.Match(xmlContent, @"<publisher[^>]*>(.+?)</publisher>", RegexOptions.Singleline);

                string? title = null;

                if (longnameMatch.Success)
                {
                    title = longnameMatch.Groups[1].Value.Trim();
                    title = Regex.Replace(title, @"\s+", " ");
                    title = System.Net.WebUtility.HtmlDecode(title);
                }

                if (string.IsNullOrWhiteSpace(title) && shortnameMatch.Success)
                {
                    title = shortnameMatch.Groups[1].Value.Trim();
                    title = Regex.Replace(title, @"\s+", " ");
                    title = System.Net.WebUtility.HtmlDecode(title);
                }

                if (!string.IsNullOrWhiteSpace(title)) metadata.Title = title;

                if (publisherMatch.Success)
                {
                    var publisher = publisherMatch.Groups[1].Value.Trim();
                    publisher = Regex.Replace(publisher, @"\s+", " ");
                    publisher = System.Net.WebUtility.HtmlDecode(publisher);

                    if (!string.IsNullOrWhiteSpace(publisher)) metadata.Developer = publisher;
                }

                try
                {
                    var doc = XDocument.Parse(xmlContent);
                    var root = doc.Root;

                    if (root != null && string.IsNullOrEmpty(metadata.Title))
                    {
                        var longname = GetNonEmptyElement(root, "longname_ko") ?? GetNonEmptyElement(root, "longname_en") ?? GetNonEmptyElement(root, "longname_ja") ?? root.Element("longname")?.Value;
                        var shortname = GetNonEmptyElement(root, "shortname_ko") ?? GetNonEmptyElement(root, "shortname_en") ?? GetNonEmptyElement(root, "shortname_ja") ?? root.Element("shortname")?.Value;
                        var finalTitle = longname ?? shortname;

                        if (!string.IsNullOrWhiteSpace(finalTitle))
                            metadata.Title = Regex.Replace(finalTitle.Trim(), @"\s+", " ");

                        if (string.IsNullOrEmpty(metadata.Developer))
                        {
                            var publisher = GetNonEmptyElement(root, "publisher_ko") ?? GetNonEmptyElement(root, "publisher_en") ?? GetNonEmptyElement(root, "publisher_ja") ?? root.Element("publisher")?.Value;

                            if (!string.IsNullOrWhiteSpace(publisher)) metadata.Developer = Regex.Replace(publisher.Trim(), @"\s+", " ");
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private static string? GetNonEmptyElement(XElement root, string elementName)
        {
            var value = root.Element(elementName)?.Value;

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        #endregion

        #region Icon Conversion

        private static byte[]? ConvertWiiUIcon(byte[] tgaData)
        {
            try
            {
                int width = tgaData[12] | (tgaData[13] << 8);
                int height = tgaData[14] | (tgaData[15] << 8);
                int bpp = tgaData[16];

                if (width != 128 || height != 128 || bpp != 32) 
                    return null;

                int headerSize = 18 + tgaData[0];
                int pixelDataSize = width * height * 4;

                if (tgaData.Length < headerSize + pixelDataSize) 
                    return null;

                byte[] bgra = new byte[pixelDataSize];

                for (int y = 0; y < height; y++)
                {
                    int srcRow = headerSize + (height - 1 - y) * width * 4;
                    int dstRow = y * width * 4;

                    Array.Copy(tgaData, srcRow, bgra, dstRow, width * 4);
                }

                var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                using var ms = new MemoryStream();
                encoder.Save(ms);

                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region WUA File System Methods

        private static ZArchiveFooter? ParseZArchiveFooter(byte[] data)
        {
            const uint MAGIC = 0x169f52d6;
            const uint VERSION1 = 0x61bf3a01;

            if (data.Length < 0x90) 
                return null;

            uint magic = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(data.Length - 4, 4));
            uint version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(data.Length - 8, 4));
            ulong totalSize = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(data.Length - 16, 8));

            if (magic != MAGIC || version != VERSION1)
                return null;

            int offset = 0;

            return new ZArchiveFooter(magic, version, totalSize, ReadSectionInfoBE(data, ref offset), ReadSectionInfoBE(data, ref offset), ReadSectionInfoBE(data, ref offset), ReadSectionInfoBE(data, ref offset), ReadSectionInfoBE(data, ref offset), ReadSectionInfoBE(data, ref offset));
        }

        private static WuaSectionInfo ReadSectionInfoBE(byte[] data, ref int offset)
        {
            var info = new WuaSectionInfo(BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8)), BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset + 8, 8)));
            offset += 16;

            return info;
        }

        private static byte[]? ReadSection(FileStream stream, WuaSectionInfo section)
        {
            if (section.Size > int.MaxValue) 
                return null;

            stream.Seek((long)section.Offset, SeekOrigin.Begin);
            byte[] data = new byte[section.Size];

            return stream.Read(data, 0, (int)section.Size) == (int)section.Size ? data : null;
        }

        private static FileEntry[]? ReadFileTree(FileStream stream, WuaSectionInfo section)
        {
            var data = ReadSection(stream, section);

            if (data == null) 
                return null;

            const int entrySize = 16;

            if (data.Length % entrySize != 0) 
                return null;

            int entryCount = data.Length / entrySize;
            var entries = new FileEntry[entryCount];

            for (int i = 0; i < entryCount; i++)
            {
                int offset = i * entrySize;

                if (offset + 16 > data.Length) 
                    break;

                uint flags = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
                uint value1 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
                uint value2 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4));
                uint value3 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 12, 4));

                bool isFile = (flags & 0x80000000) != 0;

                entries[i] = new FileEntry(flags & 0x7FFFFFFF, isFile, isFile ? value1 | ((ulong)(value3 & 0xFFFF) << 32) : value1, isFile ? value2 | ((ulong)(value3 >> 16) << 32) : value2);
            }

            return entries;
        }

        private static string? GetName(byte[] nameTable, uint nameOffset)
        {
            if (nameOffset >= nameTable.Length) 
                return null;

            int offset = (int)nameOffset;
            ushort nameLength = (ushort)(nameTable[offset] & 0x7F);

            if ((nameTable[offset] & 0x80) != 0)
            {
                if (offset + 1 >= nameTable.Length)
                    return null;

                nameLength = (ushort)(nameLength | ((ushort)nameTable[offset + 1] << 7));
                offset += 2;
            }
            else
                offset++;

            return offset + nameLength <= nameTable.Length ? Encoding.UTF8.GetString(nameTable, offset, nameLength) : null;
        }

        private static int? FindFile(FileEntry[] fileTree, byte[] nameTable, string path)
        {
            string[] parts = path.Split('/');
            int currentNode = 0;

            foreach (var part in parts)
            {
                if (fileTree[currentNode].IsFile)
                    return null;

                ulong startIndex = fileTree[currentNode].OffsetOrNodeStart;
                ulong count = fileTree[currentNode].SizeOrCount;
                bool found = false;

                for (ulong i = 0; i < count; i++)
                {
                    int nodeIndex = (int)(startIndex + i);

                    if (nodeIndex >= fileTree.Length)
                        return null;

                    var name = GetName(nameTable, fileTree[nodeIndex].NameOffset);
                    if (name != null && name.Equals(part, StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode = nodeIndex;
                        found = true;
                        break;
                    }
                }

                if (!found) 
                    return null;
            }

            return fileTree[currentNode].IsFile ? currentNode : null;
        }

        private static byte[]? ReadFileFromWua(FileStream stream, FileEntry file, ZArchiveFooter footer)
        {
            if (!file.IsFile || file.SizeOrCount > 10 * 1024 * 1024) 
                return null;

            const int BLOCK_SIZE = 0x10000;
            const int ENTRIES_PER_RECORD = 16;

            ulong fileOffset = file.OffsetOrNodeStart;
            ulong fileSize = file.SizeOrCount;

            try
            {
                var offsetRecords = ReadSection(stream, footer.SectionOffsetRecords);

                if (offsetRecords == null)
                    return null;

                int recordSize = 8 + (2 * ENTRIES_PER_RECORD);
                ulong startBlockIndex = fileOffset / BLOCK_SIZE;
                ulong endBlockIndex = (fileOffset + fileSize - 1) / BLOCK_SIZE;
                using var decompressor = new Decompressor();
                var resultData = new List<byte>();

                for (ulong blockIdx = startBlockIndex; blockIdx <= endBlockIndex; blockIdx++)
                {
                    var blockData = ReadBlockData(stream, footer, offsetRecords, recordSize, blockIdx, decompressor);

                    if (blockData == null) 
                        return null;

                    resultData.AddRange(blockData);
                }

                int offsetInDecompressed = (int)(fileOffset - (startBlockIndex * BLOCK_SIZE));

                if ((ulong)offsetInDecompressed + fileSize > (ulong)resultData.Count) 
                    return null;

                return [.. resultData.GetRange(offsetInDecompressed, (int)fileSize)];
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ReadBlockData(FileStream stream, ZArchiveFooter footer, byte[] offsetRecords, int recordSize, ulong blockIndex, Decompressor decompressor)
        {
            const int BLOCK_SIZE = 0x10000;
            const int ENTRIES_PER_RECORD = 16;

            try
            {
                int recordIndex = (int)(blockIndex / ENTRIES_PER_RECORD);
                int subIndex = (int)(blockIndex % ENTRIES_PER_RECORD);

                if (recordIndex * recordSize + recordSize > offsetRecords.Length) 
                    return null;

                int recordOffset = recordIndex * recordSize;
                ulong baseOffset = BinaryPrimitives.ReadUInt64BigEndian(offsetRecords.AsSpan(recordOffset, 8));

                ulong blockOffset = baseOffset;

                for (int i = 0; i < subIndex; i++)
                {
                    ushort sCompressedSize = BinaryPrimitives.ReadUInt16BigEndian(offsetRecords.AsSpan(recordOffset + 8 + i * 2, 2));
                    blockOffset += (ulong)(sCompressedSize + 1);
                }

                ushort currentCompressedSize = BinaryPrimitives.ReadUInt16BigEndian(offsetRecords.AsSpan(recordOffset + 8 + subIndex * 2, 2));
                int compressedSize = currentCompressedSize + 1;

                stream.Seek((long)(footer.SectionCompressedData.Offset + blockOffset), SeekOrigin.Begin);

                if (compressedSize == BLOCK_SIZE)
                {
                    byte[] uncompressed = new byte[BLOCK_SIZE];

                    return stream.Read(uncompressed, 0, BLOCK_SIZE) == BLOCK_SIZE ? uncompressed : null;
                }

                byte[] compressed = new byte[compressedSize];

                if (stream.Read(compressed, 0, compressedSize) != compressedSize) 
                    return null;

                byte[] decompressed = new byte[BLOCK_SIZE];
                int decompressedSize = decompressor.Unwrap(compressed, decompressed);

                return decompressedSize == BLOCK_SIZE ? decompressed : null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        public static void ClearCache() => _cache.Clear();
    }
}