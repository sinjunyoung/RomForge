using WiiU.Core.WUP.Crypto;
using WiiU.Core.WUP.Models;

namespace WiiU.Core.WUP.Services
{
    public static class NUSPackageFactory
    {
        private static readonly Dictionary<Content, NUSPackage> contentDictionary = [];
        private static readonly Dictionary<FST, NUSPackage> FSTDictionary = [];
        private static readonly Dictionary<TMD, NUSPackage> TMDDictionary = [];
        private static readonly Dictionary<FSTEntries, NUSPackage> FSTEntriesDictionary = [];
        private static readonly Dictionary<Contents, NUSPackage> contentsDictionary = [];

        public static NUSPackage CreatePackageFromBuiltTree(Contents contents, FST fst, AppXMLInfo appInfo, Key encryptionKey, Key encryptKeyWith)
        {
            NUSPackage nusPackage = new ();

            AddFSTDictonary(fst, nusPackage);
            AddFSTEntriesDictonary(fst.GetFSTEntries(), nusPackage);
            AddContentsDictonary(contents, nusPackage);
            AddContentDictonary(contents, nusPackage);

            fst.Update();

            Ticket ticket = new (appInfo.GetTitleID(), encryptionKey, encryptKeyWith);
            TMD tmd = new (appInfo, fst, ticket);

            tmd.Update();
            AddTMDDictonary(tmd, nusPackage);

            nusPackage.SetFST(fst);
            nusPackage.SetTicket(ticket);
            nusPackage.SetTMD(tmd);

            return nusPackage;
        }

        public static NUSPackage CreateNewPackage(NusPackageConfiguration config)
        {
            NUSPackage nusPackage = new ();

            Contents contents = new ();
            FST fst = new (contents);

            AddFSTDictonary(fst, nusPackage);

            FSTEntries entries = fst.GetFSTEntries();

            AddFSTEntriesDictonary(fst.GetFSTEntries(), nusPackage);

            FSTEntry root = entries.GetRootEntry()!;

            root.SetContent(contents.GetFSTContent());

            string dir_read = config.GetDir()!;

            ReadFiles(GetTopLevelEntries(dir_read), root);

            ContentRulesService.ApplyRules(root, contents, config.GetRules()!);

            AddContentsDictonary(contents, nusPackage);
            AddContentDictonary(contents, nusPackage);
            fst.Update();

            Ticket ticket = new (config.GetAppInfo()!.GetTitleID(), config.GetEncryptionKey()!, config.GetEncryptKeyWith()!);
            TMD tmd = new (config.GetAppInfo()!, fst, ticket);

            tmd.Update();
            AddTMDDictonary(tmd, nusPackage);
            nusPackage.SetFST(fst);
            nusPackage.SetTicket(ticket);
            nusPackage.SetTMD(tmd);

            return nusPackage;
        }

        private static void AddContentsDictonary(Contents contents, NUSPackage nusPackage)
        {
            contentsDictionary[contents] = nusPackage;
        }

        private static void AddContentDictonary(Contents contents, NUSPackage nusPackage)
        {
            foreach (Content c in contents.GetContents())
            {
                if (!contentDictionary.ContainsKey(c))
                    contentDictionary[c] = nusPackage;
            }
        }

        private static void AddTMDDictonary(TMD tmd, NUSPackage nusPackage)
        {
            TMDDictionary[tmd] = nusPackage;
        }

        private static void AddFSTDictonary(FST fst, NUSPackage nusPackage)
        {
            FSTDictionary[fst] = nusPackage;
        }

        private static void AddFSTEntriesDictonary(FSTEntries fstEntries, NUSPackage nusPackage)
        {
            FSTEntriesDictionary[fstEntries] = nusPackage;
        }

        public static NUSPackage? GetPackageByContent(Content content)
        {
            return contentDictionary.TryGetValue(content, out var pkg) ? pkg : null;
        }

        public static NUSPackage? GetPackageByFST(FST fst)
        {
            return FSTDictionary.TryGetValue(fst, out var pkg) ? pkg : null;
        }

        public static NUSPackage? GetPackageByTMD(TMD tmd)
        {
            return TMDDictionary.TryGetValue(tmd, out var pkg) ? pkg : null;
        }

        public static NUSPackage? GetPackageByContents(Contents contents)
        {
            return contentsDictionary.TryGetValue(contents, out var pkg) ? pkg : null;
        }

        public static NUSPackage? GetPackageByFSTEntires(FSTEntries fstEntries)
        {
            return FSTEntriesDictionary.TryGetValue(fstEntries, out var pkg) ? pkg : null;
        }

        private static string[] GetTopLevelEntries(string dir)
        {
            var result = new List<string>();

            result.AddRange(Directory.GetFileSystemEntries(dir));

            return [.. result];
        }

        public static void ReadFiles(string[] list, FSTEntry parent)
        {
            ReadFiles(list, parent, false);
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
                    FSTEntry newdir = new (f, notInNUSPackage);

                    parent.AddChildren(newdir);
                    ReadFiles(GetTopLevelEntries(f), newdir, notInNUSPackage);
                }
            }
        }
    }
}