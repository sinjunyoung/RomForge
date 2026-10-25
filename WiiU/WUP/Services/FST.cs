using System.Text;
using WiiU.WUP.Interfaces;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class FST(Contents contents) : IHasData
    {
        private readonly byte[] _magicbytes = [0x46, 0x53, 0x54, 0x00];
        private readonly int _unknown = 0x20;
        private int _contentCount = 0;

        private readonly byte[] _padding = new byte[0x14];
        private FSTEntries? _fileEntries = null;
        private static readonly JByteBuffer _strings = JByteBuffer.Allocate(0x300000);

        public static int CurEntryOffset = 0x00;

        private byte[]? _alignment = null;

        public Contents Contents => contents;

        public FSTEntries FSTEntries => _fileEntries ??= new FSTEntries();

        public void Update()
        {
            _strings.Clear();

            CurEntryOffset = 0;

            Contents.ResetFileOffsets();
            FSTEntries.Update();
            Contents.Update(FSTEntries);
            FSTEntries.GetRootEntry()!.RootEntryCount = FSTEntries.GetFSTEntryCount();

            _contentCount = Contents.ContentCount;
        }

        public static int GetStringPos() => _strings.Position;

        public static void AddString(string filename)
        {
            _strings.Put(Encoding.UTF8.GetBytes(filename));
            _strings.Put((byte)0x00);
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            buffer.Put(_magicbytes);
            buffer.PutInt(_unknown);
            buffer.PutInt(_contentCount);
            buffer.Put(_padding);
            buffer.Put(Contents.GetFSTContentHeaderAsData());
            buffer.Put(FSTEntries.GetAsData());

            byte[] stringData = new byte[_strings.Position];

            Array.Copy(_strings.Array(), 0, stringData, 0, _strings.Position);
            buffer.Put(stringData);
            buffer.Put(_alignment!);

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0;

            size += _magicbytes.Length;
            size += 0x04;
            size += 0x04;
            size += _padding.Length;
            size += Contents.GetFSTContentHeaderDataSize();
            size += FSTEntries.GetDataSize();
            size += _strings.Position;

            int newsize = (int)Utils.Align(size, 0x8000);

            _alignment = new byte[newsize - size];

            return newsize;
        }
    }
}