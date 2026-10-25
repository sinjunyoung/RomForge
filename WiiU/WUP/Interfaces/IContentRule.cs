using WiiU.WUP.Models;

namespace WiiU.WUP.Interfaces
{
    public interface IContentRule
    {
        string Pattern { get; set; }

        ContentDetails Details { get; set; }

        bool ContentPerMatch { get; set; }
    }
}