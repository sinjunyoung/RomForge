using RomForge.Core.Models.Naming;
using System.Text;
using System.Text.RegularExpressions;

namespace RomForge.Core.Services.Naming;

public static partial class NamingFormatSerializer
{
    [GeneratedRegex(@"\{(?<Key>[A-Za-z0-9]+)(?::(?<Option>[^{}]+))?\}")]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<NamingFormatSegment> Parse(string? format, IReadOnlyList<NamingTokenDefinition> definitions)
    {
        var segments = new List<NamingFormatSegment>();

        if (string.IsNullOrEmpty(format))
            return segments;

        int last = 0;

        foreach (Match match in TokenRegex().Matches(format))
        {
            string key = match.Groups["Key"].Value;
            var definition = definitions.FirstOrDefault(d => d.Matches(key));

            if (definition is null)
                continue;

            string? option = match.Groups["Option"].Success ? match.Groups["Option"].Value : null;

            if (option is not null && !definition.SupportsDigits)
                continue;

            if (match.Index > last)
                segments.Add(NamingFormatSegment.Literal(format[last..match.Index]));

            segments.Add(NamingFormatSegment.ForToken(definition, option));

            last = match.Index + match.Length;
        }

        if (last < format.Length)
            segments.Add(NamingFormatSegment.Literal(format[last..]));

        return segments;
    }

    public static string Serialize(IEnumerable<NamingFormatSegment> segments)
    {
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            if (segment.Token is null)
            {
                builder.Append(segment.Text);
                continue;
            }

            builder.Append('{').Append(segment.Token.Key);

            if (!string.IsNullOrEmpty(segment.Option))
                builder.Append(':').Append(segment.Option);

            builder.Append('}');
        }

        return builder.ToString();
    }
}