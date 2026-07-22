using NUSPacker.Nuspackage.Packaging;

namespace NUSPacker.Nuspackage.Interfaces
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
