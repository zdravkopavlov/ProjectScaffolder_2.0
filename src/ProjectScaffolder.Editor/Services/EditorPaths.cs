using System.IO;

namespace ProjectScaffolder.Editor.Services;

internal sealed record EditorPaths(string DataRoot)
{
    public string TemplatesRoot => Path.Combine(DataRoot, "Templates");

    public string SettingsRoot => Path.Combine(DataRoot, "Settings");

    public string LibraryPath => Path.Combine(SettingsRoot, "template-library.json");

    public string SeedMarkerPath => Path.Combine(SettingsRoot, "bundled-templates-v1.seeded");

    public static EditorPaths FromArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new EditorPaths(Path.Combine(localData, "ProjectScaffolder"));
        }

        if (args.Length != 2 || !args[0].Equals("--data-root", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException("Editor arguments must be empty or use --data-root <folder>.");
        }

        return new EditorPaths(Path.GetFullPath(args[1]));
    }
}
