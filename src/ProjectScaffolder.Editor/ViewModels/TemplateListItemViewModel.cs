namespace ProjectScaffolder.Editor.ViewModels;

internal sealed class TemplateListItemViewModel : ObservableObject
{
    private bool isExplorerEnabled;

    public TemplateListItemViewModel(TemplateEditorViewModel? editor, Guid templateId, string? errorMessage, bool explorerEnabled)
    {
        Editor = editor;
        TemplateId = templateId;
        ErrorMessage = errorMessage;
        isExplorerEnabled = explorerEnabled;
        if (editor is not null)
        {
            editor.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(TemplateEditorViewModel.DisplayName) or nameof(TemplateEditorViewModel.ItemSummary))
                {
                    OnPropertyChanged(args.PropertyName);
                }

                if (args.PropertyName == nameof(TemplateEditorViewModel.IsNew))
                {
                    OnPropertyChanged(nameof(CanToggleExplorer));
                }
            };
        }
    }

    public Guid TemplateId { get; }

    public TemplateEditorViewModel? Editor { get; }

    public string? ErrorMessage { get; }

    public bool HasError => Editor is null;

    public bool CanToggleExplorer => Editor is { IsNew: false };

    public string DisplayName => Editor?.DisplayName ?? $"Damaged template ({TemplateId:D})";

    public string ItemSummary => Editor?.ItemSummary ?? ErrorMessage ?? "Template could not be loaded";

    public bool IsExplorerEnabled
    {
        get => isExplorerEnabled;
        set
        {
            if (SetProperty(ref isExplorerEnabled, value))
            {
                ExplorerEnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? ExplorerEnabledChanged;
}
