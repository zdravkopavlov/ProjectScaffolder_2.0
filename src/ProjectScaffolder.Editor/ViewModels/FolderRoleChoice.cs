namespace ProjectScaffolder.Editor.ViewModels;

internal sealed class FolderRoleChoice(string? value, string baseLabel) : ObservableObject
{
    private string label = baseLabel;
    private bool isEnabled = true;

    public string? Value { get; } = value;

    public string BaseLabel { get; } = baseLabel;

    public string Label
    {
        get => label;
        private set => SetProperty(ref label, value);
    }

    public bool IsEnabled
    {
        get => isEnabled;
        private set => SetProperty(ref isEnabled, value);
    }

    public void SetAvailability(bool available, string? assignedPath = null)
    {
        IsEnabled = available;
        Label = available || string.IsNullOrEmpty(assignedPath)
            ? BaseLabel
            : $"{BaseLabel} — assigned to {assignedPath}";
    }

    public override string ToString() => Label;
}
