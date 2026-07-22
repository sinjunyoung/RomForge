using WiiU.Core.WUP.Services;

namespace WiiU.Core.WUP
{
    public static class Cert
    {
        public static byte[] GetCertAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0xA00);

            buffer.PutInt(0x000, 0x010003);
            buffer.PutInt(0x400, 0x010004);
            buffer.PutInt(0x700, 0x010004);

            buffer.Position = 0x240;
            buffer.Put(Utils.HexStringToByteArray("526F6F74000000000000000000000000"));
            buffer.Position = 0x280;
            buffer.Put(Utils.HexStringToByteArray("00000001434130303030303030330000"));

            buffer.Position = 0x540;
            buffer.Put(Utils.HexStringToByteArray("526F6F742D4341303030303030303300"));
            buffer.Position = 0x580;
            buffer.Put(Utils.HexStringToByteArray("00000001435030303030303030620000"));

            buffer.Position = 0x840;
            buffer.Put(Utils.HexStringToByteArray("526F6F742D4341303030303030303300"));
            buffer.Position = 0x880;
            buffer.Put(Utils.HexStringToByteArray("00000001585330303030303030630000"));

            return buffer.Array();
        }

        public static byte[] GetTMDCertAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0x700);

            buffer.PutInt(0x000, 0x010004);
            buffer.PutInt(0x300, 0x010003);

            buffer.Position = 0x140;
            buffer.Put(Utils.HexStringToByteArray("526F6F742D4341303030303030303300"));
            buffer.Position = 0x180;
            buffer.Put(Utils.HexStringToByteArray("00000001435030303030303030620000"));

            buffer.Position = 0x540;
            buffer.Put(Utils.HexStringToByteArray("526F6F74000000000000000000000000"));
            buffer.Position = 0x580;
            buffer.Put(Utils.HexStringToByteArray("00000001434130303030303030330000"));

            return buffer.Array();
        }
    }
}
