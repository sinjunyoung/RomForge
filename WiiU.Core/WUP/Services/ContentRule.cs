using WiiU.Core.WUP.Models;

namespace WiiU.Core.WUP.Services
{
    public class ContentRule
    {
        private string pattern = "";
        private ContentDetails? details = null;
        private bool contentPerMatch = false;

        public ContentRule(string pattern, ContentDetails? details, bool contentPerMatch)
        {
            SetPattern(pattern);
            SetDetails(details);
            SetContentPerMatch(contentPerMatch);
        }

        public string GetPattern() => pattern;

        public void SetPattern(string pattern)
        {
            this.pattern = pattern;
        }

        public ContentDetails? GetDetails() => details;

        public void SetDetails(ContentDetails? details)
        {
            this.details = details;
        }

        public bool IsContentPerMatch() => contentPerMatch;

        public void SetContentPerMatch(bool contentPerMatch)
        {
            this.contentPerMatch = contentPerMatch;
        }
    }
}