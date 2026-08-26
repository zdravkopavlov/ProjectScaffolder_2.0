using System.Text;
using System.Text.RegularExpressions;

namespace ProjectScaffolder.Core.Variables;

public static partial class VariableResolver
{
    public static readonly IReadOnlySet<string> BuiltInNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "project_name",
        "project_path",
        "project_id",
        "created_at_utc",
        "created_date",
        "user_name",
    };

    public static string Resolve(string input, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(values);

        string unmatchedText = VariablePattern().Replace(input, string.Empty);
        if (unmatchedText.Contains("{{", StringComparison.Ordinal) || unmatchedText.Contains("}}", StringComparison.Ordinal))
        {
            throw new VariableResolutionException("The text contains an unknown or malformed variable expression.");
        }

        StringBuilder output = new(input.Length);
        int position = 0;
        foreach (Match match in VariablePattern().Matches(input))
        {
            output.Append(input, position, match.Index - position);
            string name = match.Groups["name"].Value;
            if (!BuiltInNames.Contains(name) || !values.TryGetValue(name, out string? value))
            {
                throw new VariableResolutionException($"Unknown variable '{{{{{name}}}}}'.");
            }

            output.Append(value);
            position = match.Index + match.Length;
        }

        output.Append(input, position, input.Length - position);
        return output.ToString();
    }

    public static string? GetSyntaxError(string input)
    {
        try
        {
            Dictionary<string, string> placeholders = BuiltInNames.ToDictionary(name => name, name => name, StringComparer.Ordinal);
            _ = Resolve(input, placeholders);
            return null;
        }
        catch (VariableResolutionException exception)
        {
            return exception.Message;
        }
    }

    [GeneratedRegex("\\{\\{(?<name>[^{}]+)\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();
}
