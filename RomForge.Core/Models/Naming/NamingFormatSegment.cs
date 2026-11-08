namespace RomForge.Core.Models.Naming;

public sealed record NamingFormatSegment(string? Text, NamingTokenDefinition? Token, string? Option)
{
    public bool IsToken => Token is not null;

    public static NamingFormatSegment Literal(string text) => new(text, null, null);

    public static NamingFormatSegment ForToken(NamingTokenDefinition token, string? option) => new(null, token, option);
}