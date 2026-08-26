namespace ProjectScaffolder.Editor.ViewModels;

internal sealed record VariableOption(string Token, string Label, string Meaning);

internal sealed record ResolvedPreviewItem(
    string Icon,
    string RelativePath,
    string Kind,
    string Outcome,
    string ResolvedContent)
{
    public bool HasContent => !string.IsNullOrEmpty(ResolvedContent);
}

internal sealed record ResolvedPreviewResult(
    string TargetPath,
    string VariableSummary,
    IReadOnlyList<ResolvedPreviewItem> Items,
    IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;

    public string ErrorSummary => string.Join(Environment.NewLine, Errors.Select(error => $"• {error}"));

    public string StatusSummary => HasErrors
        ? $"Preview blocked by {Errors.Count} error{(Errors.Count == 1 ? string.Empty : "s")}."
        : $"Resolved {Items.Count} output{(Items.Count == 1 ? string.Empty : "s")} without writing to disk.";
}

internal sealed record DisposableGenerationResult(
    string WorkspacePath,
    string ProjectPath,
    string Summary);
