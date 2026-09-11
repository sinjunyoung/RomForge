namespace Vita.Core.Models;

public sealed class VitaPrepareResult
{
    public required VitaSourceItem Item { get; init; }

    public string? OutputPath { get; init; }

    public string? Error { get; init; }

    public bool Success => Error is null;
}