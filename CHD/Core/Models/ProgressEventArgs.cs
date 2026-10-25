namespace CHD.Core.Models;

public class ProgressEventArgs(int progress) : EventArgs
{
    public int Progress { get; } = progress;
}