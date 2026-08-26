using System.IO;
using Microsoft.Win32;

namespace ProjectScaffolder.Editor.Services;

internal sealed record ExplorerMenuTemplate(Guid TemplateId, string Name);

internal sealed record ExplorerMenuCommand(string KeyName, string Label, string CommandLine, bool SeparatorBefore);

internal sealed record ExplorerMenuLocation(
    string RegistryPath,
    string ExtendedSubCommandsKey,
    string CommandsRegistryPath,
    IReadOnlyList<ExplorerMenuCommand> Commands);

internal sealed record ExplorerMenuPlan(string MenuLabel, string IconPath, IReadOnlyList<ExplorerMenuLocation> Locations);

internal enum ExplorerRegistrationHealth
{
    Current,
    OutOfDate,
    PartiallyRegistered,
    NotRegistered,
    Unavailable,
}

internal interface IExplorerMenuRegistration
{
    ExplorerRegistrationHealth GetHealth(IReadOnlyList<ExplorerMenuTemplate> templates);

    ExplorerRegistrationHealth Synchronize(IReadOnlyList<ExplorerMenuTemplate> templates);
}

internal sealed class NullExplorerMenuRegistration : IExplorerMenuRegistration
{
    public ExplorerRegistrationHealth GetHealth(IReadOnlyList<ExplorerMenuTemplate> templates) => ExplorerRegistrationHealth.Current;

    public ExplorerRegistrationHealth Synchronize(IReadOnlyList<ExplorerMenuTemplate> templates) => ExplorerRegistrationHealth.Current;
}

internal static class ExplorerMenuPlanBuilder
{
    public const string BackgroundMenuPath = @"Software\Classes\Directory\Background\shell\ProjectScaffolder";
    public const string SelectedFolderMenuPath = @"Software\Classes\Directory\shell\ProjectScaffolder";
    public const string BackgroundCommandsKey = @"Directory\ContextMenus\ProjectScaffolder.Background";
    public const string SelectedFolderCommandsKey = @"Directory\ContextMenus\ProjectScaffolder.SelectedFolder";

    public static ExplorerMenuPlan Build(
        string editorPath,
        string runnerPath,
        IReadOnlyList<ExplorerMenuTemplate> templates)
    {
        string editor = Path.GetFullPath(editorPath);
        string runner = Path.GetFullPath(runnerPath);
        return new ExplorerMenuPlan(
            "New Project",
            editor,
            [
                new ExplorerMenuLocation(
                    BackgroundMenuPath,
                    BackgroundCommandsKey,
                    $@"Software\Classes\{BackgroundCommandsKey}",
                    BuildCommands(editor, runner, templates, "%V")),
                new ExplorerMenuLocation(
                    SelectedFolderMenuPath,
                    SelectedFolderCommandsKey,
                    $@"Software\Classes\{SelectedFolderCommandsKey}",
                    BuildCommands(editor, runner, templates, "%1")),
            ]);
    }

    private static List<ExplorerMenuCommand> BuildCommands(
        string editorPath,
        string runnerPath,
        IReadOnlyList<ExplorerMenuTemplate> templates,
        string targetToken)
    {
        List<ExplorerMenuCommand> commands = [];
        for (int index = 0; index < templates.Count; index++)
        {
            ExplorerMenuTemplate template = templates[index];
            commands.Add(new ExplorerMenuCommand(
                $"{index + 1:D4}-{template.TemplateId:D}",
                template.Name,
                $"{Quote(runnerPath)} --template-id {template.TemplateId:D} --target {Quote(targetToken)}",
                false));
        }

        commands.Add(new ExplorerMenuCommand("9999-Editor", "Editor", Quote(editorPath), true));
        return commands;
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

internal sealed class ExplorerMenuRegistration(string editorPath, string runnerPath) : IExplorerMenuRegistration
{
    public ExplorerRegistrationHealth GetHealth(IReadOnlyList<ExplorerMenuTemplate> templates)
    {
        try
        {
            ExplorerMenuPlan plan = ExplorerMenuPlanBuilder.Build(editorPath, runnerPath, templates);
            int existingLocations = 0;
            foreach (ExplorerMenuLocation location in plan.Locations)
            {
                using RegistryKey? menu = Registry.CurrentUser.OpenSubKey(location.RegistryPath, false);
                if (menu is null)
                {
                    continue;
                }

                existingLocations++;
                if (!IsCurrent(menu, plan, location))
                {
                    return ExplorerRegistrationHealth.OutOfDate;
                }
            }

            return existingLocations switch
            {
                0 => ExplorerRegistrationHealth.NotRegistered,
                2 => ExplorerRegistrationHealth.Current,
                _ => ExplorerRegistrationHealth.PartiallyRegistered,
            };
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ExplorerRegistrationHealth.Unavailable;
        }
    }

    public ExplorerRegistrationHealth Synchronize(IReadOnlyList<ExplorerMenuTemplate> templates)
    {
        if (!File.Exists(editorPath))
        {
            throw new FileNotFoundException("The Project Scaffolder editor executable could not be found.", editorPath);
        }

        if (templates.Count > 0 && !File.Exists(runnerPath))
        {
            throw new FileNotFoundException("The Project Scaffolder runner executable could not be found beside the editor.", runnerPath);
        }

        ExplorerMenuPlan plan = ExplorerMenuPlanBuilder.Build(editorPath, runnerPath, templates);
        foreach (ExplorerMenuLocation location in plan.Locations)
        {
            Registry.CurrentUser.DeleteSubKeyTree(location.RegistryPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(location.CommandsRegistryPath, false);
            using RegistryKey menu = Registry.CurrentUser.CreateSubKey(location.RegistryPath, true)
                ?? throw new UnauthorizedAccessException($"Could not create Explorer menu key '{location.RegistryPath}'.");
            menu.SetValue("MUIVerb", plan.MenuLabel, RegistryValueKind.String);
            menu.SetValue("Icon", plan.IconPath, RegistryValueKind.String);
            menu.SetValue("ExtendedSubCommandsKey", location.ExtendedSubCommandsKey, RegistryValueKind.String);
            using RegistryKey shell = Registry.CurrentUser.CreateSubKey($@"{location.CommandsRegistryPath}\shell", true)
                ?? throw new UnauthorizedAccessException("Could not create the Explorer submenu.");
            foreach (ExplorerMenuCommand command in location.Commands)
            {
                using RegistryKey verb = shell.CreateSubKey(command.KeyName, true)
                    ?? throw new UnauthorizedAccessException("Could not create an Explorer submenu command.");
                verb.SetValue("MUIVerb", command.Label, RegistryValueKind.String);
                if (command.SeparatorBefore)
                {
                    verb.SetValue("CommandFlags", 0x20, RegistryValueKind.DWord);
                }

                using RegistryKey commandKey = verb.CreateSubKey("command", true)
                    ?? throw new UnauthorizedAccessException("Could not create an Explorer command line.");
                commandKey.SetValue(null, command.CommandLine, RegistryValueKind.String);
            }
        }

        return ExplorerRegistrationHealth.Current;
    }

    private static bool IsCurrent(RegistryKey menu, ExplorerMenuPlan plan, ExplorerMenuLocation location)
    {
        if (!string.Equals(menu.GetValue("MUIVerb") as string, plan.MenuLabel, StringComparison.Ordinal) ||
            !string.Equals(menu.GetValue("Icon") as string, plan.IconPath, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(menu.GetValue("ExtendedSubCommandsKey") as string, location.ExtendedSubCommandsKey, StringComparison.Ordinal))
        {
            return false;
        }

        using RegistryKey? shell = Registry.CurrentUser.OpenSubKey($@"{location.CommandsRegistryPath}\shell", false);
        if (shell is null || !shell.GetSubKeyNames().SequenceEqual(location.Commands.Select(command => command.KeyName), StringComparer.Ordinal))
        {
            return false;
        }

        foreach (ExplorerMenuCommand command in location.Commands)
        {
            using RegistryKey? verb = shell.OpenSubKey(command.KeyName, false);
            using RegistryKey? commandKey = verb?.OpenSubKey("command", false);
            if (verb is null || commandKey is null ||
                !string.Equals(verb.GetValue("MUIVerb") as string, command.Label, StringComparison.Ordinal) ||
                !string.Equals(commandKey.GetValue(null) as string, command.CommandLine, StringComparison.Ordinal) ||
                (command.SeparatorBefore ? verb.GetValue("CommandFlags") is not int flags || flags != 0x20 : verb.GetValue("CommandFlags") is not null))
            {
                return false;
            }
        }

        return true;
    }
}
