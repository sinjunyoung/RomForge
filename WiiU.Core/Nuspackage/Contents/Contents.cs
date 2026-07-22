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
    /// Represents a content (one .app file) of a package
    /// </summary>
    public class Contents : IHasData
    {
        /// <summary>List of the containing "Content" elements</summary>
        private List<Content> contents = new List<Content>();
        private Content fstContent = null!;

        public Contents()
        {
            SetFSTContent(GetNewContent()); // first is always the FST.
        }

        public void SetFSTContent(Content content)
        {
            fstContent = content;
            content.SetFSTContent(true);
        }

        public Content GetFSTContent() => fstContent;

        public Content GetNewContent()
        {
            return GetNewContent(false);
        }

        /// <summary>
        /// Creates and a return a new Content element. The ID and Index will be set automatically
        /// (simply but counting up from 0)
        /// </summary>
        public Content GetNewContent(bool isHashed)
        {
            ContentDetails details = new ContentDetails(isHashed, (short)0x0000, 0x0, (short)0x0000);
            return GetNewContent(details);
        }

        public Content GetNewContent(ContentDetails details)
        {
            Content content = new Content();
            content.SetID(contents.Count);
            content.SetIndex((short)contents.Count);

            if (details.IsContent())
            {
                content.AddType(Content.TYPE_CONTENT);
            }
            if (details.IsEncrypted())
            {
                content.AddType(Content.TYPE_ENCRYPTED);
            }
            if (details.IsHashed())
            {
                content.AddType(Content.TYPE_HASHED);
            }

            content.SetEntriesFlags(details.GetEntriesFlag());

            // Extra infos for FST
            content.SetGroupID(details.GetGroupID());
            content.SetParentTitleID(details.GetParentTitleID());

            GetContents().Add(content);
            return content;
        }

        /// <summary>Returns the number of contents this collection contains</summary>
        public short GetContentCount() => (short)GetContents().Count;

        /// <summary>Returns the content info in form of a byte[]. The expected size is GetDataSize().</summary>
        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());
            foreach (Content c in GetContents())
            {
                buffer.Put(c.GetAsData());
            }
            return buffer.Array();
        }

        /// <summary>Returns the size (in bytes) the information about the contents will take in the Title Meta Data</summary>
        public int GetDataSize()
        {
            int size = 0x00;
            foreach (Content c in GetContents())
            {
                size += c.GetDataSize();
            }
            return size;
        }

        /// <summary>Returns the content info needed in the FST as a byte[]. The expected size is GetFSTContentHeaderDataSize().</summary>
        public byte[] GetFSTContentHeaderAsData()
        {
            long content_offset = 0;
            JByteBuffer buffer = JByteBuffer.Allocate(GetFSTContentHeaderDataSize());
            foreach (Content c in GetContents())
            {
                Pair<byte[], long> result = c.GetFSTContentHeaderAsData(content_offset);
                buffer.Put(result.Key);
                content_offset = result.Value;
            }
            return buffer.Array();
        }

        /// <summary>Size (in bytes) the content info will take in the FST</summary>
        public int GetFSTContentHeaderDataSize()
        {
            int size = 0;
            foreach (Content c in GetContents())
            {
                size += c.GetFSTContentHeaderDataSize();
            }
            return size;
        }

        /// <summary>Returns a List containing all contents this collection holds.</summary>
        public List<Content> GetContents()
        {
            return contents ??= new List<Content>();
        }

        /// <summary>Resets the file offsets</summary>
        public void ResetFileOffsets()
        {
            foreach (Content c in GetContents())
            {
                c.ResetFileOffsets();
            }
        }

        /// <summary>Updates the contents. Currently updating the file offsets.</summary>
        public void Update(FSTEntries fileEntries)
        {
            foreach (Content c in GetContents())
            {
                c.Update(fileEntries.GetFSTEntriesByContent(c));
            }
        }

        /// <summary>
        /// Creates all content and hash files (.app &amp; .h3). Run this BEFORE creating the tmd. Some sizes, offsets and values are changed.
        /// </summary>
        public void PackContents(string outputDir)
        {
            PackContents(outputDir, null);
        }

        /// <param name="onContentPacked">
        /// Optional: invoked right after each non-FST Content finishes packing (its
        /// GetEncryptedFileSize() is already set at that point). Purely observational - does not
        /// change what gets packed or how.
        /// </param>
        public void PackContents(string outputDir, System.Action<Content>? onContentPacked)
        {
            PackContents(outputDir, onContentPacked, null);
        }

        /// <param name="onContentBytesProcessed">
        /// Optional: invoked repeatedly while EACH content is being hashed/encrypted, as
        /// (content, phase, bytesInPhase, totalBytesInPhase) where phase is "hash" then "encrypt".
        /// Lets a caller show smooth progress even when one content dominates the total size.
        /// Purely observational.
        /// </param>
        public void PackContents(string outputDir, System.Action<Content>? onContentPacked, System.Action<Content, string, long, long>? onContentBytesProcessed)
        {
            // At first pack all non FST contents.
            foreach (Content c in GetContents())
            {
                if (!c.Equals(GetFSTContent()))
                {
                    Content current = c;
                    c.PackContentToFile(outputDir, onContentBytesProcessed == null
                        ? null
                        : (phase, done, total) => onContentBytesProcessed(current, phase, done, total));
                    onContentPacked?.Invoke(c);
                }
            }

            NUSPackage nuspackage = NUSPackageFactory.GetPackageByContents(this)!;
            Encryption encryption = nuspackage.GetEncryption();
            // Then pack the FST
            Console.WriteLine("Packing the FST into " + fstContent.GetID().ToString("X8") + ".");

            string fst_path = $"{outputDir}/{fstContent.GetID():X8}.app";
            encryption.EncryptFileWithPadding(nuspackage.GetFST(), fst_path, (short)GetFSTContent().GetID(), Content.CONTENT_FILE_PADDING);

            Console.WriteLine("-------------");
            Console.WriteLine("Packed all contents.\n\n");
        }

        public void DeleteContent(Content cur_content)
        {
            contents.Remove(cur_content);
        }
    }
}
