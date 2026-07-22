using WiiU.Core.WUP.Models;

namespace WiiU.Core.WUP.NusPackage.Interfaces
{
    public interface IContentRule
    {
        string GetPattern();

        void SetPattern(string pattern);

        ContentDetails GetDetails();

        void SetDetails(ContentDetails details);

        bool IsContentPerMatch();

        void SetContentPerMatch(bool contentPerMatch);
    }
}