using System.Text;
using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class FST(Contents contents) : IHasData
    {
        private readonly byte[] magicbytes = [ 0x46, 0x53, 0x54, 0x00 ];
        private readonly int unknown = 0x20;
        private int contentCount = 0;

        private readonly byte[] padding = new byte[0x14];
        private FSTEntries? fileEntries = null;
        private static readonly JByteBuffer strings = JByteBuffer.Allocate(0x300000);
        
        public static int CurEntryOffset = 0x00;

        private byte[]? alignment = null;

        public void Update()
        {
            strings.Clear();
            CurEntryOffset = 0;

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
            size += 0x04;
            size += 0x04;
            size += padding.Length;
            size += contents.GetFSTContentHeaderDataSize();
            size += fileEntries!.GetDataSize();
            size += strings.Position;
            
            int newsize = (int)Services.Utils.Align(size, 0x8000);
            
            alignment = new byte[newsize - size];

            return newsize;
        }

        public FSTEntries GetFSTEntries()
        {
            return fileEntries ??= new FSTEntries();
        }

        public Contents GetContents() => contents;
    }
}