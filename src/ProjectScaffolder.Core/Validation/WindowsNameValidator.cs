namespace ProjectScaffolder.Core.Validation;

public static class WindowsNameValidator
{
    private static readonly char[] InvalidCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CLOCK$", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string? GetError(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "The name cannot be empty.";
        }

        if (name.Length > 255)
        {
            return "The name cannot exceed 255 characters.";
        }

        if (name is "." or "..")
        {
            return "The name cannot be '.' or '..'.";
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return "The name cannot end with a space or period.";
        }

        if (name.Any(character => character < 32 || InvalidCharacters.Contains(character)))
        {
            return "The name contains a character that Windows does not allow.";
        }

        string deviceName = name.Split('.', 2)[0];
        if (ReservedNames.Contains(deviceName))
        {
            return $"'{deviceName}' is a reserved Windows device name.";
        }

        return null;
    }
}
