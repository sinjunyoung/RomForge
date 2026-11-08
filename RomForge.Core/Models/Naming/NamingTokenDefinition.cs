namespace RomForge.Core.Models.Naming;

public sealed record NamingTokenDefinition(string Key, string Label, string ToolTip, IReadOnlyList<NamingTokenOption>? Options = null, IReadOnlyList<string>? Aliases = null)
{
    public bool SupportsDigits => Options is { Count: > 0 };

    public NamingTokenOption? DefaultOption => SupportsDigits ? Options![0] : null;

    public bool Matches(string key) => string.Equals(Key, key, StringComparison.OrdinalIgnoreCase) || (Aliases?.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase)) ?? false);
}