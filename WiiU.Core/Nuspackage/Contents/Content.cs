using System;
using System.Collections.Generic;
using System.IO;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Fst;
using NUSPacker.Nuspackage.Interfaces;
using NUSPacker.Nuspackage.Packaging;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Contents
{
    /// <summary>
    /// Represents a content used in a package.
    /// A content holds a number of files while are saves in a .app file.
    /// The ID also used as a filename(.app)
    /// </summary>
    public class Content : IHasData
    {
        /// <summary>Represents the different types a content can be of. The actual is the combination of these types</summary>
        public const short TYPE_CONTENT = 0x2000;
        public const short TYPE_ENCRYPTED = 0x0001;
        public const short TYPE_HASHED = 0x0002;

        /// <summary>ID of this content. Unique this package</summary>
        private int ID = 0x00;

        /// <summary>Index of this content. Unique this package</summary>
        private short index = 0x00;

        /// <summary>Type of this content</summary>
        private short type = TYPE_CONTENT & TYPE_ENCRYPTED;

        /// <summary>
        /// Represents the size of this content when its packed to a .app file. (Size of the produced .app file)
        /// This can only be set after the file is packed.
        /// </summary>
        private long encryptedFileSize;

        /// <summary>
        /// If content has type TYPE_HASHED, the hash is the SHA1 hash of the corresponding .h3 file.
        /// Otherwise the hash is the SHA1 hash of the decrypted file (Filled with 0x00 until its aligned to 0x8000 (aka filesize multiple of 0x8000)).
        /// </summary>
        private byte[] SHA2 = new byte[0x14];

        /// <summary>Current fileoffset</summary>
        private long curFileOffset = 0;

        public const int ALIGNMENT_IN_CONTENT_FILE = 0x20;
        public const int CONTENT_FILE_PADDING = 0x08000;

        /// <summary>FSTEntries that are in this content</summary>
        private List<FSTEntry> entries = new List<FSTEntry>();

        /// <summary>GroupID of this Content</summary>
        private int groupID = 0;

        /// <summary>parentTitleID of this Content. Is the games when this package is an Update. But not for all contents ~</summary>
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

        public int GetFSTContentHeaderDataSize() => 0x20;

        // I don't really understand this part. Most parts are just guessed. Has something to do with the
        // Sections/Sectors on the disc. But we don't have a disc :D
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

                fst_content_size_written -= ((fst_content_size / 64) + 1) * 2; // Hopefully this is right
                if (fst_content_size_written < 0) fst_content_size_written = 0;
            }
            else
            {
                unkwn = 1;
            }

            if (IsFSTContent())
            {
                unkwn = 0;
                // Totally guessing here.
                if (fst_content_size == 1)
                {
                    fst_content_size = 0;
                }
                content_offset += fst_content_size + 2;
                fst_content_size = 0;
            }
            else
            {
                content_offset += fst_content_size;
            }

            buffer.PutInt((int)old_content_offset);
            buffer.PutInt((int)fst_content_size_written);
            buffer.PutLong(GetParentTitleID());
            buffer.PutInt(0x10, GetGroupID());
            buffer.Put(0x14, unkwn); // Seems be if this content is Hashed (2) or not(1). But always 0 for the FST

            return new Pair<byte[], long>(buffer.Array(), content_offset);
        }

        public long GetOffsetForFileAndIncrease(FSTEntry fstEntry)
        {
            long old_fileoffset = GetCurFileOffset();
            SetCurFileOffset(old_fileoffset + Utils.Utils.Align(fstEntry.GetFilesize(), ALIGNMENT_IN_CONTENT_FILE));
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

        /// <param name="onBytesProcessed">
        /// Optional: invoked repeatedly while this content is being hashed and encrypted, with
        /// (phase, bytesInPhase, totalBytesInPhase). phase is "hash" then "encrypt". Purely
        /// observational - does not change what gets packed.
        /// </param>
        public void PackContentToFile(string outputDir, System.Action<string, long, long>? onBytesProcessed)
        {
            Console.WriteLine("Packing Content " + GetID().ToString("X8"));
            Console.WriteLine();

            NUSPackage nusPackage = NUSPackageFactory.GetPackageByContent(this)!;
            Encryption encryption = nusPackage.GetEncryption();
            Console.WriteLine("Packing files into one file:");
            // At first we need to create the decrypted file.
            string decryptedFile = PackDecrypted();
            long decryptedSize = new FileInfo(decryptedFile).Length;

            Console.WriteLine();
            Console.WriteLine("Generate hashes:");
            // Calculates the hashes for the decrypted content. If the content is not hashed,
            // only the hash of the decrypted file will be calculated
            ContentHashes contentHashes = new ContentHashes(decryptedFile, IsHashed(),
                onBytesHashed: bytes => onBytesProcessed?.Invoke("hash", bytes, decryptedSize));
            string h3_path = outputDir + "/" + GetID().ToString("X8") + ".h3";
            contentHashes.SaveH3ToFile(h3_path);
            SetHash(contentHashes.GetTMDHash());
            Console.WriteLine();
            Console.WriteLine("Encrypt content (" + GetID().ToString("X8") + ")");
            string encryptedFile = PackEncrypted(outputDir, decryptedFile, contentHashes, encryption,
                onBytesEncrypted: bytes => onBytesProcessed?.Invoke("encrypt", bytes, decryptedSize));

            SetEncryptedFileSize(new FileInfo(encryptedFile).Length);

            Console.WriteLine();
            Console.WriteLine("Content " + GetID().ToString("X8") + " packed!");
            Console.WriteLine("-------------");
        }

        private string PackEncrypted(string outputDir, string decryptedFile, ContentHashes hashes, Encryption encryption, System.Action<long>? onBytesEncrypted)
        {
            string outputFilePath = $"{outputDir}/{GetID():X8}.app";
            if ((GetContentType() & TYPE_HASHED) == TYPE_HASHED)
            {
                encryption.EncryptFileHashed(decryptedFile, this, outputFilePath, hashes, onBytesEncrypted);
            }
            else
            {
                encryption.EncryptFileWithPadding(decryptedFile, this, outputFilePath, CONTENT_FILE_PADDING, onBytesEncrypted);
            }
            Console.WriteLine("Saved encrypted file to: " + outputFilePath);
            return outputFilePath;
        }

        private string PackDecrypted() // TODO: Proper error handling.
        {
            string tmp_path = $"{Settings.tmpDir}/{GetID():X8}.dec";
            using (FileStream fos = new FileStream(tmp_path, FileMode.Create, FileAccess.Write))
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
                                Console.WriteLine("FAILED"); // TODO: proper error message
                            }
                            long old_offset = cur_offset;

                            cur_offset += Utils.Utils.Align(entry.GetFilesize(), ALIGNMENT_IN_CONTENT_FILE);

                            string output = string.Format("[{0:D5}/{1:D5}] Writing at {2:x8} | FileSize: {3:x8} | {4}",
                                cnt_file, totalCount, old_offset, entry.GetFilesize(), entry.GetFilename());

                            Utils.Utils.CopyFileInto(entry.GetFile()!, fos, output);

                            int padding = (int)(cur_offset - (old_offset + entry.GetFilesize()));
                            byte[] paddingBytes = new byte[padding];
                            fos.Write(paddingBytes, 0, paddingBytes.Length);
                        }
                        else
                        {
                            Console.WriteLine($"[{cnt_file:D5}/{totalCount:D5}] Wrote folder: \"{entry.GetFilename()}\"");
                        }
                    }
                    cnt_file++;
                }
            }
            return tmp_path;
        }

        /// <param name="entries">flat list of all FSTEntry contained in this content</param>
        public void Update(List<FSTEntry>? entries)
        {
            if (entries != null)
            {
                this.entries = entries;
            }
        }

        public override bool Equals(object? other)
        {
            if (other == null || GetType() != other.GetType())
            {
                return false;
            }
            Content other_ = (Content)other;
            return ID == other_.ID;
        }

        // NOTE: deliberately NOT overriding GetHashCode() here (same as the original Java class,
        // which overrides equals() but not hashCode() either). NUSPackageFactory keys static
        // Dictionaries by Content instances; if GetHashCode() were ID-based, Content objects from
        // two DIFFERENT packages built in the same process (e.g. packing title A, then title B) but
        // sharing the same numeric ID (IDs restart at 0 per package) would hash-collide and be
        // treated as "the same" content by Dictionary lookups - silently reusing a stale
        // package/encryption key from the earlier package. Falling back to the inherited
        // identity-based GetHashCode() keeps every Content instance's bucket unique regardless of ID,
        // which is what every real call site in this codebase actually relies on (they always look
        // up using the very same object reference that was inserted).
    }
}
