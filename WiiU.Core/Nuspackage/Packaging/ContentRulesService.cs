using System;
using System.Text.RegularExpressions;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Fst;
using NUSPacker.Nuspackage.Interfaces;

namespace NUSPacker.Nuspackage.Packaging
{
    public static class ContentRulesService
    {
        public static readonly long MAX_CONTENT_LENGTH = (long)(0xBFFFFFFFL * 0.975); // Hashes take about 2 - 2.5%. Depending on the number of files the alignment/padding is added too. Needs further checks at packing

        public static long cur_content_size = 0L;

        public static Content? cur_content = null;
        public static Content? cur_content_first = null;

        public static void ApplyRules(FSTEntry root, Contents.Contents targetContents, ContentRules rules)
        {
            Console.WriteLine("-----");
            foreach (IContentRule rule in rules.GetRules())
            {
                Console.WriteLine("Apply rule " + rule.GetPattern());
                if (rule.IsContentPerMatch())
                {
                    SetNewContentRecursiveRule("", rule.GetPattern(), root, targetContents, rule);
                }
                else
                {
                    cur_content = targetContents.GetNewContent(rule.GetDetails());
                    cur_content_first = cur_content;
                    cur_content_size = 0L;
                    bool result = SetContentRecursiveRule("", rule.GetPattern(), root, targetContents, rule.GetDetails());
                    if (!result)
                    {
                        Console.WriteLine("No file matched the rule. Lets delete the content again");
                        targetContents.DeleteContent(cur_content);
                    }
                    cur_content_first = null;
                }
                Console.WriteLine("-----");
            }
        }

        /// <summary>Full-string match, matching java.util.regex.Matcher#matches() semantics.</summary>
        private static bool RegexMatches(string pattern, string input)
        {
            return Regex.IsMatch(input, "^(?:" + pattern + ")$", RegexOptions.Singleline);
        }

        private static Content? SetNewContentRecursiveRule(string path, string pattern, FSTEntry cur_entry, Contents.Contents targetContents, IContentRule rule)
        {
            path += cur_entry.GetFilename() + "/";
            Content? result = null;

            if (cur_entry.GetChildren().Count == 0)
            {
                string filePath = path;
                if (RegexMatches(pattern, filePath))
                {
                    Content result_content = targetContents.GetNewContent(rule.GetDetails());
                    result = result_content;
                }
            }
            foreach (FSTEntry child in cur_entry.GetChildren())
            {
                if (child.IsDir())
                {
                    Content? child_result = SetNewContentRecursiveRule(path, pattern, child, targetContents, rule);
                    if (child_result != null)
                    {
                        result = child_result;
                    }
                }
                else
                {
                    string filePath = path + child.GetFilename();
                    if (RegexMatches(pattern, filePath))
                    {
                        Content result_content = targetContents.GetNewContent(rule.GetDetails());
                        if (!child.IsNotInPackage()) Console.WriteLine("Set content to " + result_content.GetID().ToString("X8") + " for: " + filePath);
                        child.SetContent(result_content);
                        result = result_content;
                    }
                }
            }
            if (result != null)
            {
                cur_entry.SetContent(result);
            }
            return result;
        }

        private static bool SetContentRecursiveRule(string path, string pattern, FSTEntry cur_entry, Contents.Contents targetContents, ContentDetails contentDetails)
        {
            path += cur_entry.GetFilename() + "/";
            bool result = false;
            if (cur_entry.GetChildren().Count == 0)
            {
                string filePath = path;
                if (RegexMatches(pattern, filePath))
                {
                    if (!cur_entry.IsNotInPackage())
                        Console.WriteLine(string.Format("Set content to {0:X8} ({1:X8},{2:X8}) for: {3}", cur_content!.GetID(), cur_content_size, cur_entry.GetFilesize(), filePath));

                    if (cur_entry.GetChildren().Count == 0 /* && cur_entry.getFilename().equals("content") */) // TODO: may could cause problems. Current solution only apply to content folder.
                    {
                        cur_entry.SetContent(cur_content!);
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            foreach (FSTEntry child in cur_entry.GetChildren())
            {
                if (child.IsDir())
                {
                    bool child_result = SetContentRecursiveRule(path, pattern, child, targetContents, contentDetails);
                    if (child_result)
                    {
                        cur_entry.SetContent(cur_content_first!);
                        result = true;
                    }
                }
                else
                {
                    string filePath = path + child.GetFilename();
                    if (RegexMatches(pattern, filePath))
                    {
                        if (cur_content_size > 0 && (cur_content_size + child.GetFilesize()) > MAX_CONTENT_LENGTH)
                        {
                            Console.WriteLine("Info: Target content size is bigger than " + MAX_CONTENT_LENGTH + " bytes. Content will be splitted in mutitple files. Don't worry, I'll automatically take care of everything!");
                            cur_content = targetContents.GetNewContent(contentDetails);
                            cur_content_size = 0;
                        }
                        cur_content_size += child.GetFilesize();

                        if (!child.IsNotInPackage())
                            Console.WriteLine(string.Format("Set content to {0:X8} ({1:X8},{2:X8}) for: {3}", cur_content!.GetID(), cur_content_size, child.GetFilesize(), filePath));
                        child.SetContent(cur_content!);
                        result = true;
                    }
                }
            }
            if (result)
            {
                cur_entry.SetContent(cur_content_first!);
            }
            return result;
        }
    }
}
