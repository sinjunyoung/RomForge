using WiiU.Core.WUP.Crypto;
using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class Contents : IHasData
    {
        private List<Content> contents = [];
        private Content fstContent = null!;

        public Contents()
        {
            SetFSTContent(GetNewContent());
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

        public Content GetNewContent(bool isHashed)
        {
            ContentDetails details = new (isHashed, (short)0x0000, 0x0, (short)0x0000);

            return GetNewContent(details);
        }

        public Content GetNewContent(ContentDetails details)
        {
            Content content = new ();

            content.SetID(contents.Count);
            content.SetIndex((short)contents.Count);

            if (details.IsContent())
                content.AddType(Content.TYPE_CONTENT);

            if (details.IsEncrypted())
                content.AddType(Content.TYPE_ENCRYPTED);

            if (details.IsHashed())
                content.AddType(Content.TYPE_HASHED);

            content.SetEntriesFlags(details.GetEntriesFlag());
            content.SetGroupID(details.GetGroupID());
            content.SetParentTitleID(details.GetParentTitleID());
            GetContents().Add(content);

            return content;
        }

        public short GetContentCount() => (short)GetContents().Count;

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            foreach (Content c in GetContents())
                buffer.Put(c.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0x00;

            foreach (Content c in GetContents())
                size += c.GetDataSize();

            return size;
        }

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

        public int GetFSTContentHeaderDataSize()
        {
            int size = 0;

            foreach (Content c in GetContents())
                size += Content.GetFSTContentHeaderDataSize();

            return size;
        }

        public List<Content> GetContents()
        {
            return contents ??= [];
        }

        public void ResetFileOffsets()
        {
            foreach (Content c in GetContents())
                c.ResetFileOffsets();
        }

        public void Update(FSTEntries fileEntries)
        {
            foreach (Content c in GetContents())
                c.Update(fileEntries.GetFSTEntriesByContent(c));
        }

        public void PackContents(string outputDir)
        {
            PackContents(outputDir, null);
        }

        public void PackContents(string outputDir, Action<Content>? onContentPacked)
        {
            PackContents(outputDir, onContentPacked, null);
        }

        public void PackContents(string outputDir, Action<Content>? onContentPacked, Action<Content, string, long, long>? onContentBytesProcessed)
        {
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

            string fst_path = $"{outputDir}/{fstContent.GetID():X8}.app";
            encryption.EncryptFileWithPadding(nuspackage.GetFST(), fst_path, (short)GetFSTContent().GetID(), Content.CONTENT_FILE_PADDING);
        }

        public void DeleteContent(Content cur_content)
        {
            contents.Remove(cur_content);
        }
    }
}