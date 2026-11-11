using Common.Localization;

namespace RomForge.Core.Interfaces;

public interface IProgressTrackable
{
    int No { get; set; }

    int Progress { get; set; }

    ItemStatus State { get; set; }
}