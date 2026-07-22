namespace WiiU.Core.WUP.Models
{
    public static class Settings
    {
        public const short GROUPID_CODE = 0x0000;
        public const short GROUPID_META = 0x0400;

        public const short FSTFLAGS_CODE = 0x0000;
        public const short FSTFLAGS_META = 0x0040;
        public const short FSTFLAGS_CONTENT = 0x0400;

        public const string EncryptWithFile = "encryptKeyWith";

        public const string DefaultEncryptionKey = "13371337133713371337133713371337";
        public const string DefaultEncryptWithKey = "00000000000000000000000000000000";

        public const string PathToAppXML = "/code/app.xml";

        public static string TmpDir = "tmp";
    }
}