using System.Collections.Generic;
using NUSPacker.Nuspackage.Interfaces;

namespace NUSPacker.Nuspackage.Packaging
{
    /// <summary>
    /// Set of ContentRule objects which contains the rules for assigning file to specific content files.
    /// </summary>
    public class ContentRules
    {
        public List<IContentRule> rules = new List<IContentRule>();

        public ContentRules()
        {
        }

        /// <summary>Returns the list of all ContentRule objects</summary>
        public List<IContentRule> GetRules() => rules;

        /// <summary>Add a rule. If the rule is already in the list, this command will be ignored</summary>
        public IContentRule AddRule(IContentRule rule)
        {
            if (!rules.Contains(rule))
            {
                rules.Add(rule);
            }
            return rule;
        }

        public IContentRule CreateNewRule(string pattern, ContentDetails details, bool contentPerMatch)
        {
            IContentRule newRule = new NestedContentRule(pattern, details, contentPerMatch);
            rules.Add(newRule);
            return newRule;
        }

        public IContentRule CreateNewRule(string pattern, ContentDetails details)
        {
            return CreateNewRule(pattern, details, false);
        }

        /// <summary>
        /// Implementation of the Content Rule. Private class so it can only be created from within the ContentRules class.
        /// See the interface for proper documentation.
        /// (Named "NestedContentRule" here purely to avoid clashing, in the same namespace, with the
        /// separate top-level ContentRule class - in the original Java this is an inner class literally
        /// named ContentRule, private to ContentRules.)
        /// </summary>
        private class NestedContentRule : IContentRule
        {
            private string pattern = "";
            private ContentDetails? details = null;
            private bool contentPerMatch = false;

            public NestedContentRule(string pattern, ContentDetails? details, bool contentPerMatch)
            {
                SetPattern(pattern);
                SetDetails(details!);
                SetContentPerMatch(contentPerMatch);
            }

            public string GetPattern() => pattern;

            public void SetPattern(string pattern)
            {
                this.pattern = pattern;
            }

            public ContentDetails GetDetails() => details!;

            public void SetDetails(ContentDetails details)
            {
                this.details = details;
            }

            public bool IsContentPerMatch() => contentPerMatch;

            public void SetContentPerMatch(bool contentPerMatch)
            {
                this.contentPerMatch = contentPerMatch;
            }
        }

        public static ContentRules GetCommonRules(short contentGroup, long titleID)
        {
            ContentRules rules = new ContentRules();
            // I'm not sure of the order of the content. Maybe you can arrange it in the way we want. But this is working =)

            // At first we have the code .xml's
            ContentDetails common_details_code = new ContentDetails(false, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE); // not hashed, groupID empty, parentid empty, fstentry flags
            /*00000001*/ rules.CreateNewRule("/code/app.xml", common_details_code);
            /*00000002*/ rules.CreateNewRule("/code/cos.xml", common_details_code);

            // Then the meta.xml
            ContentDetails common_details_meta = new ContentDetails(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*00000003*/ rules.CreateNewRule("/meta/meta.xml", common_details_meta);

            // Then the rest of the meta folder except the meta.xml
            common_details_meta = new ContentDetails(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*00000004*/ rules.CreateNewRule("/meta/.*[^.xml)]+", common_details_meta);

            // But lets move the bootMovie + Logo in own files.
            common_details_meta = new ContentDetails(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*00000005*/ rules.CreateNewRule("/meta/bootMovie.h264", common_details_meta);
            /*00000006*/ rules.CreateNewRule("/meta/bootLogoTex.tga", common_details_meta);

            // ... and the manual
            ContentDetails common_details_meta_manual = new ContentDetails(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*00000007*/ rules.CreateNewRule("/meta/Manual.bfma", common_details_meta_manual);

            // ... and the images
            ContentDetails common_details_meta_images = new ContentDetails(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*00000008*/ rules.CreateNewRule("/meta/.*.jpg", common_details_meta_images);

            // Now we can assign the rpx and rpls. each gets it own content. just to be sure.
            /*00000009*/ rules.CreateNewRule("/code/.*(.rpx|.rpl)", common_details_code, true); // Each file has it own content file

            // Don't forget the preload.txt
            ContentDetails common_details_preload = new ContentDetails(true, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE); // hashed, groupID 0x400, parentid empty, fstentry flags
            /*000000??*/ rules.CreateNewRule("/code/preload.txt", common_details_preload); // Each file has it own content file

            /*000000??*/ rules.CreateNewRule("/code/fw.img", common_details_code);
            /*000000??*/ rules.CreateNewRule("/code/fw.tmd", common_details_code);
            /*000000??*/ rules.CreateNewRule("/code/htk.bin", common_details_code);
            /*000000??*/ rules.CreateNewRule("/code/rvlt.tik", common_details_code);
            /*000000??*/ rules.CreateNewRule("/code/rvlt.tmd", common_details_code);

            // And finally the content
            ContentDetails common_details_content = new ContentDetails(true, contentGroup, titleID, Settings.FSTFLAGS_CONTENT); // hashed, groupID part of titleid, parentid own titleid, fstentry flags
            /*000000??*/ rules.CreateNewRule("/content/.*", common_details_content);
            return rules;
        }
    }
}
