using System.Collections.Generic;
using System.IO;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Fst;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Packaging
{
    public static class NUSPackageFactory
    {
        private static readonly Dictionary<Content, NUSPackage> contentDictionary = new Dictionary<Content, NUSPackage>();
        private static readonly Dictionary<FST, NUSPackage> FSTDictionary = new Dictionary<FST, NUSPackage>();
        private static readonly Dictionary<TMD, NUSPackage> TMDDictionary = new Dictionary<TMD, NUSPackage>();
        private static readonly Dictionary<FSTEntries, NUSPackage> FSTEntriesDictionary = new Dictionary<FSTEntries, NUSPackage>();
        private static readonly Dictionary<Contents.Contents, NUSPackage> contentsDictionary = new Dictionary<Contents.Contents, NUSPackage>();

        /// <summary>
        /// Integration entry point for embedding the packing engine directly, bypassing the
        /// CLI's directory-scanning + regex ContentRules matching (see CreateNewPackage above).
        ///
        /// The caller must have already:
        ///  - built the FSTEntry tree under fst.GetFSTEntries().GetRootEntry() (e.g. via ReadFiles),
        ///  - created every Content via contents.GetNewContent(...),
        ///  - called SetContent(...) on every FSTEntry in the tree, files AND directories alike
        ///    (an entry with no content assigned makes packing abort).
        /// </summary>
        public static NUSPackage CreatePackageFromBuiltTree(Contents.Contents contents, FST fst, AppXMLInfo appInfo, Key encryptionKey, Key encryptKeyWith)
        {
            NUSPackage nusPackage = new NUSPackage();

            AddFSTDictonary(fst, nusPackage);
            AddFSTEntriesDictonary(fst.GetFSTEntries(), nusPackage);
            AddContentsDictonary(contents, nusPackage);
            AddContentDictonary(contents, nusPackage);

            fst.Update();

            Ticket ticket = new Ticket(appInfo.GetTitleID(), encryptionKey, encryptKeyWith);
            TMD tmd = new TMD(appInfo, fst, ticket);
            tmd.Update();
            AddTMDDictonary(tmd, nusPackage);

            nusPackage.SetFST(fst);
            nusPackage.SetTicket(ticket);
            nusPackage.SetTMD(tmd);

            return nusPackage;
        }

        public static NUSPackage CreateNewPackage(NusPackageConfiguration config)
        {
            NUSPackage nusPackage = new NUSPackage();

            Contents.Contents contents = new Contents.Contents();
            FST fst = new FST(contents);
            AddFSTDictonary(fst, nusPackage);
            FSTEntries entries = fst.GetFSTEntries();
            AddFSTEntriesDictonary(fst.GetFSTEntries(), nusPackage);

            FSTEntry root = entries.GetRootEntry()!;
            root.SetContent(contents.GetFSTContent());

            // Create FSTEntries for the given directory.
            string dir_read = config.GetDir()!;
            ReadFiles(GetTopLevelEntries(dir_read), root);

            System.Console.WriteLine("Files read. Set it to content files.");

            ContentRulesService.ApplyRules(root, contents, config.GetRules()!);

            AddContentsDictonary(contents, nusPackage);
            AddContentDictonary(contents, nusPackage);

            System.Console.WriteLine("Generating the FST.");
            fst.Update();

            System.Console.WriteLine("Generating the Ticket.");

            // titleid, key used for encryption, key used for encrypting the key.
            Ticket ticket = new Ticket(config.GetAppInfo()!.GetTitleID(), config.GetEncryptionKey()!, config.GetEncryptKeyWith()!);

            System.Console.WriteLine("Creating the TMD.");
            TMD tmd = new TMD(config.GetAppInfo()!, fst, ticket);
            tmd.Update();

            AddTMDDictonary(tmd, nusPackage);

            nusPackage.SetFST(fst);
            nusPackage.SetTicket(ticket);
            nusPackage.SetTMD(tmd);

            return nusPackage;
        }

        private static void AddContentsDictonary(Contents.Contents contents, NUSPackage nusPackage)
        {
            contentsDictionary[contents] = nusPackage;
        }

        private static void AddContentDictonary(Contents.Contents contents, NUSPackage nusPackage)
        {
            foreach (Content c in contents.GetContents())
            {
                if (!contentDictionary.ContainsKey(c))
                {
                    contentDictionary[c] = nusPackage;
                }
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

        public static NUSPackage? GetPackageByContents(Contents.Contents contents)
        {
            return contentsDictionary.TryGetValue(contents, out var pkg) ? pkg : null;
        }

        public static NUSPackage? GetPackageByFSTEntires(FSTEntries fstEntries)
        {
            return FSTEntriesDictionary.TryGetValue(fstEntries, out var pkg) ? pkg : null;
        }

        /// <summary>Java's File.listFiles() returns entries (files+dirs) of a directory; mirrored here.</summary>
        private static string[] GetTopLevelEntries(string dir)
        {
            var result = new List<string>();
            result.AddRange(Directory.GetFileSystemEntries(dir));
            return result.ToArray();
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
                {
                    parent.AddChildren(new FSTEntry(f, notInNUSPackage));
                }
            }
            foreach (string f in list)
            {
                if (Directory.Exists(f))
                {
                    FSTEntry newdir = new FSTEntry(f, notInNUSPackage);
                    parent.AddChildren(newdir);
                    ReadFiles(GetTopLevelEntries(f), newdir, notInNUSPackage);
                }
            }
        }
    }
}
