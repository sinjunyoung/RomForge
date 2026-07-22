using System;
using System.Text;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Fst;
using NUSPacker.Nuspackage.Interfaces;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage
{
    public class FST : IHasData
    {
        private readonly byte[] magicbytes = new byte[] { 0x46, 0x53, 0x54, 0x00 };
        private readonly int unknown = 0x20;
        private int contentCount = 0;

        private readonly byte[] padding = new byte[0x14];

        private Contents.Contents contents;
        private FSTEntries? fileEntries = null;

        // 3MB should be more than enough.
        private static JByteBuffer strings = JByteBuffer.Allocate(0x300000);

        /// <summary>Helper variables to build the FST</summary>
        public static int curEntryOffset = 0x00;

        private byte[]? alignment = null;

        public FST(Contents.Contents contents)
        {
            this.contents = contents;
        }

        public void Update()
        {
            strings.Clear();
            curEntryOffset = 0;

            contents.ResetFileOffsets();
            GetFSTEntries().Update();
            contents.Update(GetFSTEntries());
            GetFSTEntries().GetRootEntry()!.SetEntryCount(GetFSTEntries().GetFSTEntryCount());

            contentCount = contents.GetContentCount();
        }

        public static int GetStringPos() => strings.Position;

        public static void AddString(string filename)
        {
            strings.Put(Encoding.UTF8.GetBytes(filename));
            strings.Put((byte)0x00);
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());
            buffer.Put(magicbytes);
            buffer.PutInt(unknown);
            buffer.PutInt(contentCount);
            buffer.Put(padding);
            buffer.Put(contents.GetFSTContentHeaderAsData());
            buffer.Put(fileEntries!.GetAsData());
            byte[] stringData = new byte[strings.Position];
            Array.Copy(strings.Array(), 0, stringData, 0, strings.Position);
            buffer.Put(stringData);
            buffer.Put(alignment!);
            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0;
            size += magicbytes.Length;
            size += 0x04; // unknown
            size += 0x04; // contentCount
            size += padding.Length;
            size += contents.GetFSTContentHeaderDataSize();
            size += fileEntries!.GetDataSize();
            size += strings.Position;
            int newsize = (int)Utils.Utils.Align(size, 0x8000);
            alignment = new byte[newsize - size];
            return newsize;
        }

        public FSTEntries GetFSTEntries()
        {
            return fileEntries ??= new FSTEntries();
        }

        public Contents.Contents GetContents() => contents;
    }
}
