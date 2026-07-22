using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class ContentRules
    {
        public List<IContentRule> rules = [];

        public ContentRules()
        {
        }

        public List<IContentRule> GetRules() => rules;

        public IContentRule AddRule(IContentRule rule)
        {
            if (!rules.Contains(rule))
                rules.Add(rule);

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
            ContentRules rules = new ();

            ContentDetails common_details_code = new (false, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE);

            rules.CreateNewRule("/code/app.xml", common_details_code);
            rules.CreateNewRule("/code/cos.xml", common_details_code);
            
            ContentDetails common_details_meta = new (true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/meta.xml", common_details_meta);

            common_details_meta = new (true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/.*[^.xml)]+", common_details_meta);
                        
            common_details_meta = new (true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/bootMovie.h264", common_details_meta);
            rules.CreateNewRule("/meta/bootLogoTex.tga", common_details_meta);
            
            ContentDetails common_details_meta_manual = new (true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/Manual.bfma", common_details_meta_manual);

            ContentDetails common_details_meta_images = new (true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/.*.jpg", common_details_meta_images);
            rules.CreateNewRule("/code/.*(.rpx|.rpl)", common_details_code, true);

            ContentDetails common_details_preload = new (true, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE);

            rules.CreateNewRule("/code/preload.txt", common_details_preload);
            rules.CreateNewRule("/code/fw.img", common_details_code);
            rules.CreateNewRule("/code/fw.tmd", common_details_code);
            rules.CreateNewRule("/code/htk.bin", common_details_code);
            rules.CreateNewRule("/code/rvlt.tik", common_details_code);
            rules.CreateNewRule("/code/rvlt.tmd", common_details_code);
            
            ContentDetails common_details_content = new (true, contentGroup, titleID, Settings.FSTFLAGS_CONTENT);

            rules.CreateNewRule("/content/.*", common_details_content);

            return rules;
        }
    }
}