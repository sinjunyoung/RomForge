using System.Diagnostics;
using System.Text.RegularExpressions;
using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public static class ContentRulesService
    {
        public static readonly long MAX_CONTENT_LENGTH = (long)(0xBFFFFFFFL * 0.975);
        public static long CurContentSize = 0L;
        public static Content? CurContent = null;
        public static Content? CurContentFirst = null;

        public static void ApplyRules(FSTEntry root, Contents targetContents, ContentRules rules)
        {
            foreach (IContentRule rule in rules.GetRules())
            {
                if (rule.IsContentPerMatch())
                    SetNewContentRecursiveRule(string.Empty, rule.GetPattern(), root, targetContents, rule);
                else
                {
                    CurContent = targetContents.GetNewContent(rule.GetDetails());
                    CurContentFirst = CurContent;
                    CurContentSize = 0L;
                    
                    bool result = SetContentRecursiveRule(string.Empty, rule.GetPattern(), root, targetContents, rule.GetDetails());

                    if (!result)
                        targetContents.DeleteContent(CurContent);

                    CurContentFirst = null;
                }
            }
        }

        private static bool RegexMatches(string pattern, string input)
        {
            return Regex.IsMatch(input, "^(?:" + pattern + ")$", RegexOptions.Singleline);
        }

        private static Content? SetNewContentRecursiveRule(string path, string pattern, FSTEntry cur_entry, Contents targetContents, IContentRule rule)
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
                        result = child_result;
                }
                else
                {
                    string filePath = path + child.GetFilename();

                    if (RegexMatches(pattern, filePath))
                    {
                        Content result_content = targetContents.GetNewContent(rule.GetDetails());

                        if (!child.IsNotInPackage()) 
                            Debug.WriteLine("Set content to " + result_content.GetID().ToString("X8") + " for: " + filePath);

                        child.SetContent(result_content);

                        result = result_content;
                    }
                }
            }

            if (result != null)
                cur_entry.SetContent(result);

            return result;
        }

        private static bool SetContentRecursiveRule(string path, string pattern, FSTEntry cur_entry, Contents targetContents, ContentDetails contentDetails)
        {
            path += cur_entry.GetFilename() + "/";
            bool result = false;

            if (cur_entry.GetChildren().Count == 0)
            {
                string filePath = path;
                if (RegexMatches(pattern, filePath))
                {
                    if (!cur_entry.IsNotInPackage())
                        Debug.WriteLine(string.Format("Set content to {0:X8} ({1:X8},{2:X8}) for: {3}", CurContent!.GetID(), CurContentSize, cur_entry.GetFilesize(), filePath));

                    if (cur_entry.GetChildren().Count == 0)
                        cur_entry.SetContent(CurContent!);

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
                        cur_entry.SetContent(CurContentFirst!);
                        result = true;
                    }
                }
                else
                {
                    string filePath = path + child.GetFilename();

                    if (RegexMatches(pattern, filePath))
                    {
                        if (CurContentSize > 0 && (CurContentSize + child.GetFilesize()) > MAX_CONTENT_LENGTH)
                        {   
                            CurContent = targetContents.GetNewContent(contentDetails);
                            CurContentSize = 0;
                        }

                        CurContentSize += child.GetFilesize();

                        if (!child.IsNotInPackage())
                            Debug.WriteLine(string.Format("Set content to {0:X8} ({1:X8},{2:X8}) for: {3}", CurContent!.GetID(), CurContentSize, child.GetFilesize(), filePath));

                        child.SetContent(CurContent!);
                        result = true;
                    }
                }
            }

            if (result)
                cur_entry.SetContent(CurContentFirst!);

            return result;
        }
    }
}