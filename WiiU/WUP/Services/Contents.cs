using System.Collections;
using WiiU.WUP.Crypto;
using WiiU.WUP.Interfaces;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class Contents : IHasData, IEnumerable<Content>
    {
        private readonly List<Content> _contents = [];

        public Contents() => FSTContent = GetNewContent();

        public Content FSTContent
        {
            get;
            set
            {
                field = value;
                value.IsFSTContent = true;
            }
        }

        public short ContentCount => (short)_contents.Count;

        public IEnumerator<Content> GetEnumerator() => _contents.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _contents.GetEnumerator();

        public Content GetNewContent() => GetNewContent(false);

        public Content GetNewContent(bool isHashed)
        {
            ContentDetails details = new(isHashed, (short)0x0000, 0x0, (short)0x0000);
            return GetNewContent(details);
        }

        public Content GetNewContent(ContentDetails details)
        {
            Content content = new()
            {
                ID = _contents.Count,
                Index = (short)_contents.Count
            };

            if (details.IsContent)
                content.AddType(Content.TYPE_CONTENT);

            if (details.IsEncrypted)
                content.AddType(Content.TYPE_ENCRYPTED);

            if (details.IsHashed)
                content.AddType(Content.TYPE_HASHED);

            content.EntriesFlags = details.EntriesFlag;
            content.GroupID = details.GroupID;
            content.ParentTitleID = details.ParentTitleID;

            _contents.Add(content);

            return content;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            foreach (Content c in this)
                buffer.Put(c.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0x00;

            foreach (Content c in this)
                size += c.GetDataSize();

            return size;
        }

        public byte[] GetFSTContentHeaderAsData()
        {
            long content_offset = 0;
            JByteBuffer buffer = JByteBuffer.Allocate(GetFSTContentHeaderDataSize());

            foreach (Content c in this)
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

            foreach (Content c in this)
                size += Content.GetFSTContentHeaderDataSize();

            return size;
        }

        public void ResetFileOffsets()
        {
            foreach (Content c in this)
                c.ResetFileOffsets();
        }

        public void Update(FSTEntries fileEntries)
        {
            foreach (Content c in this)
                c.Update(fileEntries.GetFSTEntriesByContent(c));
        }

        public void PackContents(string outputDir, Action<Content>? onContentPacked, Action<Content, string, long, long>? onContentBytesProcessed)
        {
            foreach (Content c in this)
            {
                if (!c.Equals(FSTContent))
                {
                    Content current = c;

                    c.PackContentToFile(outputDir, onContentBytesProcessed == null
                        ? null
                        : (phase, done, total) => onContentBytesProcessed(current, phase, done, total));
                    onContentPacked?.Invoke(c);
                }
            }

            NUSPackage nuspackage = NUSPackageFactory.GetPackageByContents(this)!;
            Encryption encryption = nuspackage.Encryption;

            string fst_path = $"{outputDir}/{FSTContent.ID:X8}.app";
            encryption.EncryptFileWithPadding(nuspackage.FST, fst_path, (short)FSTContent.ID, Content.CONTENT_FILE_PADDING);
        }

        public void DeleteContent(Content curContent) => _contents.Remove(curContent);
    }
}