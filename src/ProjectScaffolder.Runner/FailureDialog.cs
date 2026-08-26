using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace ProjectScaffolder.Runner;

internal static class FailureDialog
{
    public static void Show(
        Guid? templateId,
        string? targetPath,
        string message,
        bool? rollbackCompleted,
        IReadOnlyList<string>? inspectionPaths,
        string? logPath)
    {
        StringBuilder text = new();
        text.AppendLine(message);
        text.AppendLine();
        text.AppendFormat(CultureInfo.InvariantCulture, "Template: {0}", templateId?.ToString("D") ?? "Unknown").AppendLine();
        text.AppendFormat(CultureInfo.InvariantCulture, "Target: {0}", targetPath ?? "Unknown").AppendLine();

        if (rollbackCompleted is not null)
        {
            text.AppendLine(rollbackCompleted.Value
                ? "Rollback: Completed"
                : "Rollback: Incomplete — inspect the paths listed in the log");
        }

        if (inspectionPaths is { Count: > 0 })
        {
            text.AppendFormat(CultureInfo.InvariantCulture, "Needs inspection: {0}", inspectionPaths[0]).AppendLine();
        }

        text.AppendLine(logPath is null
            ? "Diagnostic log: Could not be written"
            : $"Diagnostic log: {logPath}");

        MessageBox.Show(
            text.ToString(),
            "Project Scaffolder",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
    }
}
