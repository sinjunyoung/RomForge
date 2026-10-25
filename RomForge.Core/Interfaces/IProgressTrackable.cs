namespace RomForge.Core.Interfaces;

public interface IProgressTrackable
{
    int No { get; set; }

    int Progress { get; set; }

    string Status { get; set; }
}