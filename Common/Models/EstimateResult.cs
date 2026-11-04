namespace Common.Models;

public sealed record EstimateResult(long InputBytes, long OutputBytes, long MarginBytes)
{
    public long SavedBytes => InputBytes - OutputBytes;

    public double SavedFraction => InputBytes > 0 ? (double)SavedBytes / InputBytes : 0;
}