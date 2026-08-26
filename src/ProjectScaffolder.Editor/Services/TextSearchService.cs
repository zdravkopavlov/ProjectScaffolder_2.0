using System.Text;

namespace ProjectScaffolder.Editor.Services;

internal static class TextSearchService
{
    public static int FindNext(string text, string query, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(query))
        {
            return -1;
        }

        int start = Math.Clamp(startIndex, 0, text.Length);
        int index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
        return index >= 0 || start == 0
            ? index
            : text.IndexOf(query, 0, start, StringComparison.OrdinalIgnoreCase);
    }

    public static (string Text, int ReplacementCount) ReplaceAll(string text, string query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        if (string.IsNullOrEmpty(query))
        {
            return (text, 0);
        }

        StringBuilder result = new(text.Length);
        int position = 0;
        int count = 0;
        while (position < text.Length)
        {
            int match = text.IndexOf(query, position, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                break;
            }

            result.Append(text, position, match - position);
            result.Append(replacement);
            position = match + query.Length;
            count++;
        }

        result.Append(text, position, text.Length - position);
        return (result.ToString(), count);
    }
}
