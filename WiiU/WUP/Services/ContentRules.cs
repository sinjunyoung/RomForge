using WiiU.WUP.Interfaces;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class ContentRules
    {
        public List<IContentRule> Rules { get; set; } = [];

        public ContentRules() { }

        public IContentRule CreateNewRule(string pattern, ContentDetails details, bool contentPerMatch)
        {
            IContentRule newRule = new NestedContentRule(pattern, details, contentPerMatch);

            Rules.Add(newRule);

            return newRule;
        }

        public IContentRule CreateNewRule(string pattern, ContentDetails details) => CreateNewRule(pattern, details, false);

        private class NestedContentRule(string pattern, ContentDetails? details, bool contentPerMatch) : IContentRule
        {
            public string Pattern { get; set; } = pattern;

            public ContentDetails Details { get; set; } = details!;

            public bool ContentPerMatch { get; set; } = contentPerMatch;
        }

        public static ContentRules GetCommonRules(short contentGroup, long titleID)
        {
            ContentRules rules = new();

            ContentDetails common_details_code = new(false, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE);

            rules.CreateNewRule("/code/app.xml", common_details_code);
            rules.CreateNewRule("/code/cos.xml", common_details_code);

            ContentDetails common_details_meta = new(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/meta.xml", common_details_meta);

            common_details_meta = new(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/.*[^.xml)]+", common_details_meta);

            common_details_meta = new(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/bootMovie.h264", common_details_meta);
            rules.CreateNewRule("/meta/bootLogoTex.tga", common_details_meta);

            ContentDetails common_details_meta_manual = new(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/Manual.bfma", common_details_meta_manual);

            ContentDetails common_details_meta_images = new(true, Settings.GROUPID_META, 0x0L, Settings.FSTFLAGS_META);

            rules.CreateNewRule("/meta/.*.jpg", common_details_meta_images);
            rules.CreateNewRule("/code/.*(.rpx|.rpl)", common_details_code, true);

            ContentDetails common_details_preload = new(true, Settings.GROUPID_CODE, 0x0L, Settings.FSTFLAGS_CODE);

            rules.CreateNewRule("/code/preload.txt", common_details_preload);
            rules.CreateNewRule("/code/fw.img", common_details_code);
            rules.CreateNewRule("/code/fw.tmd", common_details_code);
            rules.CreateNewRule("/code/htk.bin", common_details_code);
            rules.CreateNewRule("/code/rvlt.tik", common_details_code);
            rules.CreateNewRule("/code/rvlt.tmd", common_details_code);

            ContentDetails common_details_content = new(true, contentGroup, titleID, Settings.FSTFLAGS_CONTENT);

            rules.CreateNewRule("/content/.*", common_details_content);

            return rules;
        }
    }
}