namespace NUSPacker
{
    public static class Settings
    {
        public const short GROUPID_CODE = 0x0000;
        public const short GROUPID_META = 0x0400;

        public const short FSTFLAGS_CODE = 0x0000;
        public const short FSTFLAGS_META = 0x0040;
        public const short FSTFLAGS_CONTENT = 0x0400;

        public const string encryptWithFile = "encryptKeyWith";

        public const string defaultEncryptionKey = "13371337133713371337133713371337";
        public const string defaultEncryptWithKey = "00000000000000000000000000000000";

        public const string pathToAppXML = "/code/app.xml";

        // NOTE: was `const string tmpDir = "tmp"` in the original CLI tool (relative to the
        // process's working directory, fine for a command-line run). Made mutable so an embedding
        // app can point this at a real writable temp directory instead.
        public static string tmpDir = "tmp";
    }
}
