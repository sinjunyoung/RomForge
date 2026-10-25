using WiiU.WUP.Crypto;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public static class NUSPackageFactory
    {
        private static readonly Dictionary<Content, NUSPackage> _contentDictionary = [];
        private static readonly Dictionary<FST, NUSPackage> _fstDictionary = [];
        private static readonly Dictionary<TMD, NUSPackage> _tmdDictionary = [];
        private static readonly Dictionary<FSTEntries, NUSPackage> _fstEntriesDictionary = [];
        private static readonly Dictionary<Contents, NUSPackage> _contentsDictionary = [];

        public static NUSPackage CreatePackageFromBuiltTree(Contents contents, FST fst, AppXMLInfo appInfo, Key encryptionKey, Key encryptKeyWith)
        {
            NUSPackage nusPackage = new();

            AddFSTDictionary(fst, nusPackage);
            AddFSTEntriesDictionary(fst.FSTEntries, nusPackage);
            AddContentsDictionary(contents, nusPackage);
            AddContentDictionary(contents, nusPackage);

            fst.Update();

            Ticket ticket = new(appInfo.TitleID, encryptionKey, encryptKeyWith);
            TMD tmd = new(appInfo, fst, ticket);

            tmd.UpdateContents();
            AddTMDDictionary(tmd, nusPackage);

            nusPackage.FST = fst;
            nusPackage.Ticket = ticket;
            nusPackage.TMD = tmd;

            return nusPackage;
        }

        private static void AddContentsDictionary(Contents contents, NUSPackage nusPackage) => _contentsDictionary[contents] = nusPackage;

        private static void AddContentDictionary(Contents contents, NUSPackage nusPackage)
        {
            foreach (Content c in contents)
            {
                if (!_contentDictionary.ContainsKey(c))
                    _contentDictionary[c] = nusPackage;
            }
        }

        private static void AddTMDDictionary(TMD tmd, NUSPackage nusPackage) => _tmdDictionary[tmd] = nusPackage;

        private static void AddFSTDictionary(FST fst, NUSPackage nusPackage) => _fstDictionary[fst] = nusPackage;

        private static void AddFSTEntriesDictionary(FSTEntries fstEntries, NUSPackage nusPackage) => _fstEntriesDictionary[fstEntries] = nusPackage;

        public static NUSPackage? GetPackageByContent(Content content) => _contentDictionary.TryGetValue(content, out var pkg) ? pkg : null;

        public static NUSPackage? GetPackageByContents(Contents contents) => _contentsDictionary.TryGetValue(contents, out var pkg) ? pkg : null;

        private static string[] GetTopLevelEntries(string dir)
        {
            var result = new List<string>();

            result.AddRange(Directory.GetFileSystemEntries(dir));

            return [.. result];
        }

        public static void ReadFiles(string[] list, FSTEntry parent, bool notInNUSPackage)
        {
            foreach (string f in list)
            {
                if (!Directory.Exists(f))
                    parent.AddChildren(new FSTEntry(f, notInNUSPackage));
            }

            foreach (string f in list)
            {
                if (Directory.Exists(f))
                {
                    FSTEntry newDir = new(f, notInNUSPackage);

                    parent.AddChildren(newDir);
                    ReadFiles(GetTopLevelEntries(f), newDir, notInNUSPackage);
                }
            }
        }
    }
}