using System.Diagnostics;
using WiiU.Core.WUP.Crypto;
using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class Content : IHasData
    {
        public const short TYPE_CONTENT = 0x2000;
        public const short TYPE_ENCRYPTED = 0x0001;
        public const short TYPE_HASHED = 0x0002;
        
        public const int ALIGNMENT_IN_CONTENT_FILE = 0x20;
        public const int CONTENT_FILE_PADDING = 0x08000;

        private int ID = 0x00;
        private short index = 0x00;
        private short type = TYPE_CONTENT & TYPE_ENCRYPTED;
        private long encryptedFileSize;
        private byte[] SHA2 = new byte[0x14];
        private long curFileOffset = 0;
        private List<FSTEntry> entries = [];
        private int groupID = 0;
        private long parentTitleID = 0;
        private short entriesFlags = 0x0000;
        private bool isFSTContentFlag;

        internal Content()
        {
        }

        public int GetID() => ID;

        public void SetID(int id)
        {
            ID = id;
        }

        public short GetContentType() => type;

        public void AddType(short type)
        {
            this.type |= type;
        }

        public void RemoveType(short type)
        {
            this.type &= (short)~type;
        }

        public void SetType(short type)
        {
            this.type = type;
        }

        public short GetIndex() => index;

        public void SetIndex(short index)
        {
            this.index = index;
        }

        public long GetParentTitleID() => parentTitleID;

        public void SetParentTitleID(long parentTitleID)
        {
            this.parentTitleID = parentTitleID;
        }

        public int GetGroupID() => groupID;

        public void SetGroupID(int groupID)
        {
            this.groupID = groupID;
        }

        private long GetCurFileOffset() => curFileOffset;

        private void SetCurFileOffset(long curFileOffset)
        {
            this.curFileOffset = curFileOffset;
        }

        public void SetEncryptedFileSize(long size)
        {
            encryptedFileSize = size;
        }

        public long GetEncryptedFileSize() => encryptedFileSize;

        public void SetHash(byte[] hash)
        {
            SHA2 = hash;
        }

        public byte[] GetHash() => SHA2;

        public bool IsHashed() => (GetContentType() & TYPE_HASHED) == TYPE_HASHED;

        public bool IsFSTContent() => isFSTContentFlag;

        public void SetFSTContent(bool isFSTContent)
        {
            isFSTContentFlag = isFSTContent;
        }

        public void SetEntriesFlags(short entriesFlag)
        {
            entriesFlags = entriesFlag;
        }

        public short GetEntriesFlags() => entriesFlags;

        public static int GetFSTContentHeaderDataSize() => 0x20;
        
        public Pair<byte[], long> GetFSTContentHeaderAsData(long old_content_offset)
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetFSTContentHeaderDataSize());

            byte unkwn;
            long content_offset = old_content_offset;

            long fst_content_size = GetEncryptedFileSize() / CONTENT_FILE_PADDING;
            long fst_content_size_written = fst_content_size;

            if (IsHashed())
            {
                unkwn = 2;

                fst_content_size_written -= ((fst_content_size / 64) + 1) * 2;
                if (fst_content_size_written < 0) fst_content_size_written = 0;
            }
            else
            {
                unkwn = 1;
            }

            if (IsFSTContent())
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
            buffer.PutLong(GetParentTitleID());
            buffer.PutInt(0x10, GetGroupID());
            buffer.Put(0x14, unkwn);

            return new Pair<byte[], long>(buffer.Array(), content_offset);
        }

        public long GetOffsetForFileAndIncrease(FSTEntry fstEntry)
        {
            long old_fileoffset = GetCurFileOffset();
            SetCurFileOffset(old_fileoffset + Services.Utils.Align(fstEntry.GetFilesize(), ALIGNMENT_IN_CONTENT_FILE));

            return old_fileoffset;
        }

        public void ResetFileOffsets()
        {
            curFileOffset = 0;
        }

        private List<FSTEntry> GetFSTEntries() => entries;

        public int GetFSTEntryNumber() => entries.Count;

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            buffer.PutInt(GetID());
            buffer.PutShort(GetIndex());
            buffer.PutShort(GetContentType());
            buffer.PutLong(GetEncryptedFileSize());
            buffer.Put(GetHash());

            return buffer.Array();
        }

        public int GetDataSize() => 0x30;

        public void PackContentToFile(string outputDir)
        {
            PackContentToFile(outputDir, null);
        }

        public void PackContentToFile(string outputDir, Action<string, long, long>? onBytesProcessed)
        {
            NUSPackage nusPackage = NUSPackageFactory.GetPackageByContent(this)!;
            Encryption encryption = nusPackage.GetEncryption();
            string decryptedFile = PackDecrypted(onBytesCopied: onBytesProcessed == null ? null : (done, total) => onBytesProcessed("stage", done, total));
            long decryptedSize = new FileInfo(decryptedFile).Length;
            ContentHashes contentHashes = new (decryptedFile, IsHashed(), onBytesHashed: bytes => onBytesProcessed?.Invoke("hash", bytes, decryptedSize));

            string h3_path = outputDir + "/" + GetID().ToString("X8") + ".h3";
            contentHashes.SaveH3ToFile(h3_path);
            SetHash(contentHashes.GetTMDHash());

            string encryptedFile = PackEncrypted(outputDir, decryptedFile, contentHashes, encryption, onBytesEncrypted: bytes => onBytesProcessed?.Invoke("encrypt", bytes, decryptedSize));

            SetEncryptedFileSize(new FileInfo(encryptedFile).Length);
        }

        private string PackEncrypted(string outputDir, string decryptedFile, ContentHashes hashes, Encryption encryption, Action<long>? onBytesEncrypted)
        {
            string outputFilePath = $"{outputDir}/{GetID():X8}.app";

            if ((GetContentType() & TYPE_HASHED) == TYPE_HASHED)
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
                if (!e.IsNotInPackage() && e.IsFile())
                    totalFileBytes += e.GetFilesize();
            }

            long copiedSoFar = 0;
            string tmp_path = $"{Settings.TmpDir}/{GetID():X8}.dec";

            using (FileStream fos = new(tmp_path, FileMode.Create, FileAccess.Write))
            {
                int totalCount = GetFSTEntryNumber();
                int cnt_file = 1;
                long cur_offset = 0;

                foreach (FSTEntry entry in GetFSTEntries())
                {
                    if (!entry.IsNotInPackage())
                    {
                        if (entry.IsFile())
                        {
                            if (cur_offset != entry.GetFileOffset())
                            {
                                Debug.WriteLine("FAILED");
                            }
                            long old_offset = cur_offset;

                            cur_offset += Services.Utils.Align(entry.GetFilesize(), ALIGNMENT_IN_CONTENT_FILE);

                            string output = string.Format("[{0:D5}/{1:D5}] Writing at {2:x8} | FileSize: {3:x8} | {4}", cnt_file, totalCount, old_offset, entry.GetFilesize(), entry.GetFilename());

                            Func<Stream>? streamFactory = entry.GetStreamFactory();

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
                                Services.Utils.CopyFileInto(entry.GetFile()!, fos);
                                copiedSoFar += entry.GetFilesize();
                                onBytesCopied?.Invoke(copiedSoFar, totalFileBytes);
                            }

                            int padding = (int)(cur_offset - (old_offset + entry.GetFilesize()));
                            byte[] paddingBytes = new byte[padding];
                            fos.Write(paddingBytes, 0, paddingBytes.Length);
                        }
                        else
                        {
                            Debug.WriteLine($"[{cnt_file:D5}/{totalCount:D5}] Wrote folder: \"{entry.GetFilename()}\"");
                        }
                    }
                    cnt_file++;
                }
            }
            return tmp_path;
        }

        public void Update(List<FSTEntry>? entries)
        {
            if (entries != null)
                this.entries = entries;
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