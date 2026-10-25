using System.Diagnostics;
using WiiU.WUP.Crypto;
using WiiU.WUP.Interfaces;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class Content : IHasData
    {
        public const short TYPE_CONTENT = 0x2000;

        public const short TYPE_ENCRYPTED = 0x0001;

        public const short TYPE_HASHED = 0x0002;

        public const int ALIGNMENT_IN_CONTENT_FILE = 0x20;

        public const int CONTENT_FILE_PADDING = 0x08000;

        private long _curFileOffset = 0;
        private List<FSTEntry> _entries = [];
        private short _type = TYPE_CONTENT & TYPE_ENCRYPTED;

        internal Content() { }

        public int ID { get; set; } = 0x00;

        public short Index { get; set; } = 0x00;

        public short ContentType
        {
            get => _type;
            set => _type = value;
        }

        public long EncryptedFileSize { get; set; }

        public byte[] Hash { get; set; } = new byte[0x14];

        public int GroupID { get; set; } = 0;

        public long ParentTitleID { get; set; } = 0;

        public short EntriesFlags { get; set; } = 0x0000;

        public bool IsFSTContent { get; set; }

        private long CurFileOffset
        {
            get => _curFileOffset;
            set => _curFileOffset = value;
        }

        public void AddType(short type) => _type |= type;

        public bool IsHashed() => (ContentType & TYPE_HASHED) == TYPE_HASHED;

        public static int GetFSTContentHeaderDataSize() => 0x20;

        public Pair<byte[], long> GetFSTContentHeaderAsData(long old_content_offset)
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetFSTContentHeaderDataSize());

            byte unkwn;
            long content_offset = old_content_offset;
            long fst_content_size = EncryptedFileSize / CONTENT_FILE_PADDING;
            long fst_content_size_written = fst_content_size;

            if (IsHashed())
            {
                unkwn = 2;
                fst_content_size_written -= ((fst_content_size / 64) + 1) * 2;

                if (fst_content_size_written < 0)
                    fst_content_size_written = 0;
            }
            else
                unkwn = 1;

            if (IsFSTContent)
            {
                unkwn = 0;

                if (fst_content_size == 1)
                    fst_content_size = 0;

                content_offset += fst_content_size + 2;
            }
            else
                content_offset += fst_content_size;

            buffer.PutInt((int)old_content_offset);
            buffer.PutInt((int)fst_content_size_written);
            buffer.PutLong(ParentTitleID);
            buffer.PutInt(0x10, GroupID);
            buffer.Put(0x14, unkwn);

            return new Pair<byte[], long>(buffer.Array(), content_offset);
        }

        public long GetOffsetForFileAndIncrease(FSTEntry fstEntry)
        {
            long old_fileoffset = CurFileOffset;

            CurFileOffset = old_fileoffset + Utils.Align(fstEntry.Filesize, ALIGNMENT_IN_CONTENT_FILE);

            return old_fileoffset;
        }

        public void ResetFileOffsets() => CurFileOffset = 0;

        private List<FSTEntry> GetFSTEntries() => _entries;

        public int GetFSTEntryNumber() => _entries.Count;

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            buffer.PutInt(ID);
            buffer.PutShort(Index);
            buffer.PutShort(ContentType);
            buffer.PutLong(EncryptedFileSize);
            buffer.Put(Hash);

            return buffer.Array();
        }

        public int GetDataSize() => 0x30;

        public void PackContentToFile(string outputDir, Action<string, long, long>? onBytesProcessed)
        {
            NUSPackage nusPackage = NUSPackageFactory.GetPackageByContent(this)!;
            Encryption encryption = nusPackage.Encryption;
            string decryptedFile = PackDecrypted(onBytesCopied: onBytesProcessed == null ? null : (done, total) => onBytesProcessed("stage", done, total));
            long decryptedSize = new FileInfo(decryptedFile).Length;
            ContentHashes contentHashes = new(decryptedFile, IsHashed(), onBytesHashed: bytes => onBytesProcessed?.Invoke("hash", bytes, decryptedSize));
            string h3_path = outputDir + "/" + ID.ToString("X8") + ".h3";

            contentHashes.SaveH3ToFile(h3_path);

            Hash = contentHashes.TMDHash;

            string encryptedFile = PackEncrypted(outputDir, decryptedFile, contentHashes, encryption, onBytesEncrypted: bytes => onBytesProcessed?.Invoke("encrypt", bytes, decryptedSize));

            EncryptedFileSize = new FileInfo(encryptedFile).Length;
        }

        private string PackEncrypted(string outputDir, string decryptedFile, ContentHashes hashes, Encryption encryption, Action<long>? onBytesEncrypted)
        {
            string outputFilePath = $"{outputDir}/{ID:X8}.app";

            if ((ContentType & TYPE_HASHED) == TYPE_HASHED)
                encryption.EncryptFileHashed(decryptedFile, this, outputFilePath, hashes, onBytesEncrypted);
            else
                encryption.EncryptFileWithPadding(decryptedFile, this, outputFilePath, CONTENT_FILE_PADDING, onBytesEncrypted);

            return outputFilePath;
        }

        private string PackDecrypted(Action<long, long>? onBytesCopied)
        {
            long totalFileBytes = 0;

            foreach (FSTEntry e in GetFSTEntries())
            {
                if (!e.IsNotInPackage && e.IsFile)
                    totalFileBytes += e.Filesize;
            }

            long copiedSoFar = 0;
            string tmp_path = $"{Settings.TmpDir}/{ID:X8}.dec";
            using (FileStream fos = new(tmp_path, FileMode.Create, FileAccess.Write))
            {
                int totalCount = GetFSTEntryNumber();
                int cnt_file = 1;
                long cur_offset = 0;

                foreach (FSTEntry entry in GetFSTEntries())
                {
                    if (!entry.IsNotInPackage)
                    {
                        if (entry.IsFile)
                        {
                            if (cur_offset != entry.FileOffset)
                                Debug.WriteLine("FAILED");

                            long old_offset = cur_offset;

                            cur_offset += Utils.Align(entry.Filesize, ALIGNMENT_IN_CONTENT_FILE);

                            string output = string.Format("[{0:D5}/{1:D5}] Writing at {2:x8} | FileSize: {3:x8} | {4}", cnt_file, totalCount, old_offset, entry.Filesize, entry.Filename);

                            Func<Stream>? streamFactory = entry.StreamFactory;

                            if (streamFactory != null)
                            {
                                using Stream src = streamFactory();
                                byte[] copyBuffer = new byte[1 << 20];
                                int read;

                                while ((read = src.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
                                {
                                    fos.Write(copyBuffer, 0, read);
                                    copiedSoFar += read;
                                    onBytesCopied?.Invoke(copiedSoFar, totalFileBytes);
                                }
                            }
                            else
                            {
                                Utils.CopyFileInto(entry.FilePath!, fos);
                                copiedSoFar += entry.Filesize;
                                onBytesCopied?.Invoke(copiedSoFar, totalFileBytes);
                            }

                            int padding = (int)(cur_offset - (old_offset + entry.Filesize));
                            byte[] paddingBytes = new byte[padding];

                            fos.Write(paddingBytes, 0, paddingBytes.Length);
                        }
                        else
                            Debug.WriteLine($"[{cnt_file:D5}/{totalCount:D5}] Wrote folder: \"{entry.Filename}\"");
                    }

                    cnt_file++;
                }
            }

            return tmp_path;
        }

        public void Update(List<FSTEntry>? entries)
        {
            if (entries != null)
                _entries = entries;
        }

        public override bool Equals(object? other)
        {
            if (other == null || GetType() != other.GetType())
                return false;

            Content other_ = (Content)other;

            return ID == other_.ID;
        }

        public override int GetHashCode() => 0;

    }
}