namespace ProjectScaffolder.Editor.Services;

internal sealed record EditorStartupOptions(
    EditorPaths Paths,
    bool SmokeSelectFirstNode,
    bool EnableExplorerRegistration,
    bool RepairExplorerRegistration)
{
    public static EditorStartupOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? dataRoot = null;
        bool smokeSelect = false;
        bool repairExplorerRegistration = false;
        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--data-root" when dataRoot is null && index + 1 < args.Length:
                    dataRoot = args[++index];
                    break;
                case "--smoke-select-first" when !smokeSelect:
                    smokeSelect = true;
                    break;
                case "--register-explorer-menu" when !repairExplorerRegistration:
                    repairExplorerRegistration = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown or incomplete editor option '{args[index]}'.");
            }
        }

        EditorPaths paths = dataRoot is null
            ? EditorPaths.FromArguments([])
            : EditorPaths.FromArguments(["--data-root", dataRoot]);
        if (repairExplorerRegistration && (dataRoot is not null || smokeSelect))
        {
            throw new ArgumentException("--register-explorer-menu cannot be combined with test-only editor options.");
        }

        return new EditorStartupOptions(paths, smokeSelect, dataRoot is null, repairExplorerRegistration);
    }
}
