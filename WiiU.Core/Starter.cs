using System;
using System.IO;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Packaging;
using NUSPacker.Utils;

namespace NUSPacker
{
    public class Starter
    {
        public static void Main(string[] args)
        {
            Console.Write("NUSPacker 0.3-i");
            new CompileDate().PrintDate();
            Console.WriteLine();
            Console.WriteLine();

            Directory.CreateDirectory(Settings.tmpDir);

            string inputPath = "output";
            string outputPath = "output";
            Directory.CreateDirectory(outputPath);

            string encryptionKey = "";
            string encryptKeyWith = "";

            long titleID = 0x0L;
            long OSVersion = 0x000500101000400AL;
            int appType = unchecked((int)0x80000000);
            short titleVersion = 0;

            bool skipXMLReading = false;

            if (args.Length == 0)
            {
                Console.WriteLine("Provide at least the in and out parameter.");
                Console.WriteLine();
                ShowHelp();
                Environment.Exit(0);
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-in")
                {
                    if (args.Length > i)
                    {
                        inputPath = args[i + 1];
                        i++;
                    }
                }
                else if (args[i] == "-out")
                {
                    if (args.Length > i)
                    {
                        outputPath = args[i + 1];
                        Directory.CreateDirectory(outputPath);
                        i++;
                    }
                }
                else if (args[i] == "-tID")
                {
                    if (args.Length > i)
                    {
                        titleID = Utils.Utils.HexStringToLong(args[i + 1]);
                        i++;
                    }
                }
                else if (args[i] == "-OSVersion")
                {
                    if (args.Length > i)
                    {
                        OSVersion = Utils.Utils.HexStringToLong(args[i + 1]);
                        i++;
                    }
                }
                else if (args[i] == "-appType")
                {
                    if (args.Length > i)
                    {
                        appType = (int)Utils.Utils.HexStringToLong(args[i + 1]);
                        i++;
                    }
                }
                else if (args[i] == "-titleVersion")
                {
                    if (args.Length > i)
                    {
                        titleVersion = (short)Utils.Utils.HexStringToLong(args[i + 1]);
                        i++;
                    }
                }
                else if (args[i] == "-encryptionKey")
                {
                    if (args.Length > i)
                    {
                        encryptionKey = args[i + 1];
                        i++;
                    }
                }
                else if (args[i] == "-encryptKeyWith")
                {
                    if (args.Length > i)
                    {
                        encryptKeyWith = args[i + 1];
                        i++;
                    }
                }
                else if (args[i] == "-skipXMLParsing")
                {
                    skipXMLReading = true;
                }
                else if (args[i] == "-help")
                {
                    ShowHelp();
                    Environment.Exit(0);
                }
            }

            if (!Directory.Exists(Path.Combine(inputPath, "code")) ||
                !Directory.Exists(Path.Combine(inputPath, "content")) ||
                !Directory.Exists(Path.Combine(inputPath, "meta")))
            {
                Console.Error.WriteLine("Invalid input dir (" + Path.GetFullPath(inputPath) + "): It's missing either the code, content or meta folder.");
                Environment.Exit(0);
            }

            AppXMLInfo appInfo = new AppXMLInfo();
            // Set command line values in case the XML reading fails.
            appInfo.SetTitleID(titleID);
            appInfo.SetGroupID((short)((titleID >> 8) & 0xFFFF));
            appInfo.SetAppType(appType);
            appInfo.SetOSVersion(OSVersion);
            appInfo.SetTitleVersion(titleVersion);

            if (encryptionKey == "" || encryptionKey.Length != 32)
            {
                encryptionKey = Settings.defaultEncryptionKey;
                Console.WriteLine("Empty or invalid encryption provided. Will use " + encryptionKey + " instead");
            }
            Console.WriteLine();

            if (encryptKeyWith == "" || encryptKeyWith.Length != 32)
            {
                Console.WriteLine("Will try to load the encryptionWith key from the file \"" + Settings.encryptWithFile + "\"");
                encryptKeyWith = LoadEncryptWithKey();
            }
            if (encryptKeyWith == "" || encryptKeyWith.Length != 32)
            {
                encryptKeyWith = Settings.defaultEncryptWithKey;
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine("WARNING:Empty or invalid encryptWith key provided. Will use " + encryptKeyWith + " instead");
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!");
            }

            Console.WriteLine();

            string appxmlPath = inputPath + Settings.pathToAppXML;

            if (!skipXMLReading)
            {
                try
                {
                    Console.WriteLine("Parsing app.xml (values will be overwritten. Use the -skipXMLParsing argument to disable it)");
                    XMLParser parser = new XMLParser();
                    parser.LoadDocument(appxmlPath);
                    appInfo = parser.GetAppXMLInfo();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    Console.Error.WriteLine("Error while parsing the app.xml from path \"" + Settings.pathToAppXML + "\"");
                }
            }
            else
            {
                Console.WriteLine("Skipped app.xml parsing");
            }

            short content_group = appInfo.GetGroupID();
            titleID = appInfo.GetTitleID();

            long parentID = titleID & ~0x0000000F00000000L;

            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("Input            : \"" + inputPath + "\"");
            Console.WriteLine("Output           : \"" + outputPath + "\"");
            Console.WriteLine("TitleID          : " + appInfo.GetTitleID().ToString("X16"));
            Console.WriteLine("GroupID          : " + appInfo.GetGroupID().ToString("X4"));
            Console.WriteLine("ParentID         : " + parentID.ToString("X16"));
            Console.WriteLine("AppType          : " + appInfo.GetAppType().ToString("X8"));
            Console.WriteLine("OSVersion        : " + appInfo.GetOSVersion().ToString("X16"));
            Console.WriteLine("Encryption key   : " + encryptionKey);
            Console.WriteLine("Encrypt key with : " + encryptKeyWith);
            Console.WriteLine();

            Console.WriteLine("---");

            ContentRules rules = ContentRules.GetCommonRules(content_group, parentID);

            NusPackageConfiguration config = new NusPackageConfiguration(inputPath, appInfo, new Key(encryptionKey), new Key(encryptKeyWith), rules);
            // Create a new nuspackage!
            NUSPackage nuspackage = NUSPackageFactory.CreateNewPackage(config);
            // And now to pack it to .app files
            nuspackage.PackContents(outputPath);
            nuspackage.PrintTicketInfos();

            // Clean up
            Utils.Utils.DeleteDir(Settings.tmpDir);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("help:");
            Console.WriteLine("-in             ; is the dir where you have your decrypted data. Make this pointing to the root folder with the folder code,content and meta.");
            Console.WriteLine("-out            ; Where the installable package will be saves");
            Console.WriteLine("");
            Console.WriteLine("(optional! will be parsed from app.xml if missing)");
            Console.WriteLine("-tID            ; titleId of this package. Will be saved in the TMD and provided as 00050000XXXXXXXX");
            Console.WriteLine("-OSVersion      ; target OS version");
            Console.WriteLine("-appType        ; app type");
            Console.WriteLine("-skipXMLParsing ; disables the app.xml parsing");
            Console.WriteLine("");
            Console.WriteLine("(optional! defaults values will be used if missing (or loaded from external file))");
            Console.WriteLine("-encryptionKey  ; the key that is used to encrypt the package");
            Console.WriteLine("-encryptKeyWith ; the key that is used to encrypt the encryption key");
            Console.WriteLine("");
        }

        // TODO: do it in a clean way
        public static string LoadEncryptWithKey()
        {
            if (!File.Exists(Settings.encryptWithFile)) return "";
            string key = "";
            try
            {
                using StreamReader in_ = new StreamReader(Settings.encryptWithFile);
                key = in_.ReadLine() ?? "";
            }
            catch (IOException)
            {
                Console.WriteLine("Failed to read \"" + Settings.encryptWithFile + "\"");
            }

            return key;
        }
    }
}
