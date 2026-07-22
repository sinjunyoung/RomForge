using WiiU.Core.WUP.Crypto;

namespace WiiU.Core.WUP.Services
{
    public class NusPackageConfiguration
    {
        private string? dir;
        private AppXMLInfo? appInfo;
        private Key? encryptionKey;
        private Key? encryptKeyWith;
        private ContentRules? rules;
        private string? fullGameDir = null;

        public NusPackageConfiguration(string dir, AppXMLInfo appInfo, Key encryptionKey, Key encryptKeyWith, ContentRules rules)
        {
            SetDir(dir);
            SetAppInfo(appInfo);
            SetEncryptionKey(encryptionKey);
            SetEncryptKeyWith(encryptKeyWith);
            SetRules(rules);
        }

        public string? GetDir() => dir;

        public void SetDir(string? dir)
        {
            this.dir = dir;
        }

        public AppXMLInfo? GetAppInfo() => appInfo;

        public void SetAppInfo(AppXMLInfo? appInfo)
        {
            this.appInfo = appInfo;
        }

        public Key? GetEncryptionKey() => encryptionKey;

        public void SetEncryptionKey(Key? encryptionKey)
        {
            this.encryptionKey = encryptionKey;
        }

        public Key? GetEncryptKeyWith() => encryptKeyWith;

        public void SetEncryptKeyWith(Key? encryptKeyWith)
        {
            this.encryptKeyWith = encryptKeyWith;
        }

        public ContentRules? GetRules() => rules;

        public void SetRules(ContentRules? rules)
        {
            this.rules = rules;
        }

        public string? GetFullGameDir() => fullGameDir;

        public void SetFullGameDir(string? fullGameDir)
        {
            this.fullGameDir = fullGameDir;
        }
    }
}