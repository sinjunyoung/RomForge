namespace Common.Models;

public record struct ProgressInfo(int Percent, string Label, string TitleId, string Speed, string TimeInfo, ProgressStage Stage = ProgressStage.None);