using RomForge.Core.Models.Switch;

namespace RomForge.Core.ViewModels.Switch;

public class Lz4Stats : ViewModelBase
{
    public double EstimatedRatio { get; private set; } = double.NegativeInfinity;

    public string EstimatedRatioText { get; private set => SetProperty(ref field, value); } = "-";

    public string ActualRatioText { get; private set => SetProperty(ref field, value); } = "-";

    public string CompressedSizeText { get; private set => SetProperty(ref field, value); } = "-";

    public void MarkAnalyzing()
    {
        EstimatedRatio = double.NegativeInfinity;
        EstimatedRatioText = "분석 중...";
    }

    public void ApplyEstimate(Lz4FileEstimate estimate)
    {
        EstimatedRatio = estimate.Kind == Lz4EstimateKind.Estimated ? estimate.SavedFraction : double.NegativeInfinity;
        EstimatedRatioText = estimate.Kind switch
        {
            Lz4EstimateKind.Estimated => FormatRatio(estimate.SavedFraction),
            Lz4EstimateKind.AlreadyCompressed => "압축됨",
            _ => "-"
        };
    }

    public void ApplyEstimateFailed()
    {
        EstimatedRatio = double.NegativeInfinity;
        EstimatedRatioText = "추정 실패";
    }

    public void ApplyActual(long inputBytes, long outputBytes, string compressedSizeText)
    {
        ActualRatioText = inputBytes > 0 ? FormatRatio((double)(inputBytes - outputBytes) / inputBytes) : "-";
        CompressedSizeText = compressedSizeText;
    }

    public void ResetActual()
    {
        ActualRatioText = "-";
        CompressedSizeText = "-";
    }

    private static string FormatRatio(double fraction) => $"{fraction * 100:F1}%";
}