using System.Collections.ObjectModel;
using System.IO;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.ViewModels;

internal sealed class MainViewModel : ObservableObject
{
    private readonly TemplateLibraryStore store;
    private TemplateListItemViewModel? selectedListItem;
    private EditorNodeViewModel? selectedNode;
    private string statusMessage = "Loading templates...";

    public MainViewModel(TemplateLibraryStore store)
    {
        this.store = store;
        NewTemplateCommand = new RelayCommand(NewTemplate);
        DuplicateTemplateCommand = new RelayCommand(DuplicateTemplate, () => SelectedTemplate is not null);
        SaveTemplateCommand = new AsyncRelayCommand(SaveTemplateAsync, () => SelectedTemplate is { IsDirty: true, IsValid: true });
        AddFolderCommand = new RelayCommand(AddFolder, () => SelectedTemplate is not null);
        AddTextFileCommand = new RelayCommand(AddTextFile, () => SelectedTemplate is not null);
        DeleteNodeCommand = new RelayCommand(DeleteNode, () => SelectedTemplate is not null && SelectedNode is not null);
        MoveNodeUpCommand = new RelayCommand(() => MoveNode(-1), CanMoveNodeUp);
        MoveNodeDownCommand = new RelayCommand(() => MoveNode(1), CanMoveNodeDown);
    }

    public ObservableCollection<TemplateListItemViewModel> Templates { get; } = [];

    public IReadOnlyList<TextFileEncodingKind> TextEncodings { get; } = Enum.GetValues<TextFileEncodingKind>();

    public IReadOnlyList<LineEndingKind> LineEndings { get; } = Enum.GetValues<LineEndingKind>();

    public IReadOnlyList<FileCollisionPolicy> FileCollisionPolicies { get; } = Enum.GetValues<FileCollisionPolicy>();

    public RelayCommand NewTemplateCommand { get; }

    public RelayCommand DuplicateTemplateCommand { get; }

    public AsyncRelayCommand SaveTemplateCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand AddTextFileCommand { get; }

    public RelayCommand DeleteNodeCommand { get; }

    public RelayCommand MoveNodeUpCommand { get; }

    public RelayCommand MoveNodeDownCommand { get; }

    public TemplateListItemViewModel? SelectedListItem
    {
        get => selectedListItem;
        set
        {
            if (SetProperty(ref selectedListItem, value))
            {
                SelectedNode = null;
                OnPropertyChanged(nameof(SelectedTemplate));
                OnPropertyChanged(nameof(HasSelectedTemplate));
                NotifyCommandStates();
                StatusMessage = value?.HasError == true
                    ? value.ErrorMessage ?? "This template could not be loaded."
                    : value?.Editor?.ValidationStatus ?? "No template selected";
            }
        }
    }

    public TemplateEditorViewModel? SelectedTemplate => SelectedListItem?.Editor;

    public bool HasSelectedTemplate => SelectedTemplate is not null;

    public EditorNodeViewModel? SelectedNode
    {
        get => selectedNode;
        set
        {
            if (SetProperty(ref selectedNode, value))
            {
                OnPropertyChanged(nameof(HasSelectedNode));
                NotifyCommandStates();
            }
        }
    }

    public bool HasSelectedNode => SelectedNode is not null;

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public async Task InitializeAsync()
    {
        LoadedTemplateLibrary loaded = await store.LoadAsync().ConfigureAwait(true);
        foreach (StoredTemplate stored in loaded.Templates)
        {
            TemplateEditorViewModel? editor = stored.Document is null ? null : new TemplateEditorViewModel(stored.Document);
            TemplateListItemViewModel item = new(editor, stored.TemplateId, stored.ErrorMessage, loaded.ExplorerTemplateIds.Contains(stored.TemplateId));
            item.ExplorerEnabledChanged += ExplorerEnabledChanged;
            if (editor is not null)
            {
                editor.PropertyChanged += SelectedTemplatePropertyChanged;
            }

            Templates.Add(item);
        }

        SelectedListItem = Templates.FirstOrDefault(item => !item.HasError) ?? Templates.FirstOrDefault();
        StatusMessage = Templates.Count == 0 ? "Create your first template." : SelectedTemplate?.ValidationStatus ?? "A damaged template is selected.";
    }

    public async Task<bool> SaveCurrentTemplateAsync()
    {
        if (SelectedTemplate is null || !SelectedTemplate.IsDirty)
        {
            return true;
        }

        if (!SelectedTemplate.IsValid)
        {
            StatusMessage = "Correct the validation errors before saving.";
            return false;
        }

        try
        {
            TemplateDocument document = SelectedTemplate.CreateSaveDocument();
            await store.SaveTemplateAsync(document).ConfigureAwait(true);
            SelectedTemplate.AcceptSaved(document);
            StatusMessage = "Template saved.";
            NotifyCommandStates();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ContractValidationException)
        {
            StatusMessage = $"Save failed: {exception.Message}";
            return false;
        }
    }

    public async Task<bool> SaveAllDirtyTemplatesAsync()
    {
        foreach (TemplateListItemViewModel item in Templates.Where(item => item.Editor?.IsDirty == true))
        {
            TemplateEditorViewModel editor = item.Editor!;
            if (!editor.IsValid)
            {
                SelectedListItem = item;
                StatusMessage = "Correct the validation errors before saving.";
                return false;
            }

            try
            {
                TemplateDocument document = editor.CreateSaveDocument();
                await store.SaveTemplateAsync(document).ConfigureAwait(true);
                editor.AcceptSaved(document);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ContractValidationException)
            {
                SelectedListItem = item;
                StatusMessage = $"Save failed: {exception.Message}";
                return false;
            }
        }

        StatusMessage = "All templates saved.";
        NotifyCommandStates();
        return true;
    }

    public async Task<bool> RemoveSelectedTemplateAsync()
    {
        TemplateListItemViewModel? item = SelectedListItem;
        if (item is null)
        {
            return false;
        }

        int index = Templates.IndexOf(item);
        try
        {
            string? trashPath = item.Editor?.IsNew == true
                ? null
                : await store.RemoveTemplateAsync(item.TemplateId).ConfigureAwait(true);
            Templates.Remove(item);
            SelectedListItem = Templates.Count == 0
                ? null
                : Templates[Math.Min(index, Templates.Count - 1)];
            StatusMessage = trashPath is null
                ? "Unsaved template removed."
                : "Template removed and its package moved to the application Trash folder.";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not remove template: {exception.Message}";
            return false;
        }
    }

    private void NewTemplate()
    {
        TemplateEditorViewModel editor = TemplateEditorViewModel.CreateNew();
        AddEditor(editor);
        StatusMessage = "New unsaved template.";
    }

    private void DuplicateTemplate()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        TemplateEditorViewModel duplicate = TemplateEditorViewModel.Duplicate(SelectedTemplate);
        AddEditor(duplicate);
        StatusMessage = "Duplicated as a new unsaved template.";
    }

    private void AddEditor(TemplateEditorViewModel editor)
    {
        TemplateListItemViewModel item = new(editor, editor.TemplateId, null, false);
        item.ExplorerEnabledChanged += ExplorerEnabledChanged;
        editor.PropertyChanged += SelectedTemplatePropertyChanged;
        Templates.Add(item);
        SelectedListItem = item;
    }

    private async Task SaveTemplateAsync() => await SaveCurrentTemplateAsync().ConfigureAwait(true);

    private void AddFolder()
    {
        TemplateEditorViewModel template = SelectedTemplate!;
        EditorNodeViewModel folder = EditorNodeViewModel.CreateFolder(GetUniqueName("New Folder"));
        if (SelectedNode?.Kind == EditorNodeKind.Folder)
        {
            SelectedNode.AddChild(folder);
        }
        else
        {
            template.AddRoot(folder);
        }

        template.NotifyStructureChanged();
        SelectedNode = folder;
    }

    private void AddTextFile()
    {
        TemplateEditorViewModel template = SelectedTemplate!;
        EditorNodeViewModel file = EditorNodeViewModel.CreateTextFile(GetUniqueName("New File"));
        if (SelectedNode?.Kind == EditorNodeKind.Folder)
        {
            SelectedNode.AddChild(file);
        }
        else
        {
            template.AddRoot(file);
        }

        template.NotifyStructureChanged();
        SelectedNode = file;
    }

    private void DeleteNode()
    {
        TemplateEditorViewModel template = SelectedTemplate!;
        EditorNodeViewModel node = SelectedNode!;
        if (node.Parent is null)
        {
            template.Nodes.Remove(node);
        }
        else
        {
            node.Parent.Children.Remove(node);
        }

        SelectedNode = null;
        template.NotifyStructureChanged();
    }

    private void MoveNode(int offset)
    {
        EditorNodeViewModel node = SelectedNode!;
        ObservableCollection<EditorNodeViewModel> siblings = node.Parent?.Children ?? SelectedTemplate!.Nodes;
        int oldIndex = siblings.IndexOf(node);
        int newIndex = oldIndex + offset;
        siblings.Move(oldIndex, newIndex);

        SelectedTemplate!.NotifyStructureChanged();
        NotifyCommandStates();
    }

    private bool CanMoveNodeUp() => GetSelectedNodeIndex() > 0;

    private bool CanMoveNodeDown()
    {
        if (SelectedNode is null || SelectedTemplate is null)
        {
            return false;
        }

        ObservableCollection<EditorNodeViewModel> siblings = SelectedNode.Parent?.Children ?? SelectedTemplate.Nodes;
        int index = siblings.IndexOf(SelectedNode);
        return index >= 0 && index < siblings.Count - 1;
    }

    private int GetSelectedNodeIndex()
    {
        if (SelectedNode is null || SelectedTemplate is null)
        {
            return -1;
        }

        ObservableCollection<EditorNodeViewModel> siblings = SelectedNode.Parent?.Children ?? SelectedTemplate.Nodes;
        return siblings.IndexOf(SelectedNode);
    }

    private string GetUniqueName(string baseName)
    {
        IEnumerable<string> names = SelectedNode?.Kind == EditorNodeKind.Folder
            ? SelectedNode.Children.Select(node => node.Name)
            : SelectedTemplate!.Nodes.Select(node => node.Name);
        HashSet<string> existing = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{baseName} {suffix}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private async void ExplorerEnabledChanged(object? sender, EventArgs e)
    {
        if (sender is not TemplateListItemViewModel item || item.Editor?.IsNew == true)
        {
            StatusMessage = "Save a new template before enabling it in Explorer.";
            return;
        }

        try
        {
            await store.SetExplorerEnabledAsync(item.TemplateId, item.IsExplorerEnabled).ConfigureAwait(true);
            StatusMessage = item.IsExplorerEnabled
                ? "Template marked for the Explorer menu. Registration arrives in Milestone 4."
                : "Template removed from the future Explorer menu.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not update Explorer state: {exception.Message}";
        }
    }

    private void SelectedTemplatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TemplateEditorViewModel template && template == SelectedTemplate &&
            e.PropertyName is nameof(TemplateEditorViewModel.IsDirty) or nameof(TemplateEditorViewModel.IsValid))
        {
            NotifyCommandStates();
            StatusMessage = template.ValidationStatus;
        }
    }

    private void NotifyCommandStates()
    {
        DuplicateTemplateCommand.NotifyCanExecuteChanged();
        SaveTemplateCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        AddTextFileCommand.NotifyCanExecuteChanged();
        DeleteNodeCommand.NotifyCanExecuteChanged();
        MoveNodeUpCommand.NotifyCanExecuteChanged();
        MoveNodeDownCommand.NotifyCanExecuteChanged();
    }
}
