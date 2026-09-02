using System.Collections.ObjectModel;
using System.IO;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Core.Validation;
using ProjectScaffolder.Core.Variables;
using ProjectScaffolder.Editor.Services;

namespace ProjectScaffolder.Editor.ViewModels;

internal sealed class MainViewModel : ObservableObject
{
    private readonly TemplateLibraryStore store;
    private readonly IExplorerMenuRegistration explorerMenu;
    private TemplateListItemViewModel? selectedListItem;
    private EditorNodeViewModel? selectedNode;
    private string statusMessage = "Loading templates...";

    public MainViewModel(TemplateLibraryStore store, IExplorerMenuRegistration? explorerMenu = null)
    {
        this.store = store;
        this.explorerMenu = explorerMenu ?? new NullExplorerMenuRegistration();
        NewTemplateCommand = new RelayCommand(NewTemplate);
        DuplicateTemplateCommand = new AsyncRelayCommand(DuplicateTemplateAsync, () => SelectedTemplate is not null);
        SaveTemplateCommand = new AsyncRelayCommand(SaveTemplateAsync, () => SelectedTemplate is { IsDirty: true, IsValid: true });
        AddFolderCommand = new RelayCommand(AddFolder, () => SelectedTemplate is not null);
        AddTextFileCommand = new RelayCommand(AddTextFile, () => SelectedTemplate is not null);
        DeleteNodeCommand = new RelayCommand(DeleteNode, () => SelectedTemplate is not null && SelectedNode is not null);
        RenameNodeCommand = new RelayCommand(RenameNode, () => SelectedNode is not null);
    }

    public ObservableCollection<TemplateListItemViewModel> Templates { get; } = [];

    public IReadOnlyList<ChoiceOption<TextFileEncodingKind>> TextEncodingChoices { get; } =
    [
        new(TextFileEncodingKind.Utf8, "UTF-8"),
        new(TextFileEncodingKind.Utf8Bom, "UTF-8 with BOM"),
        new(TextFileEncodingKind.Utf16LittleEndian, "UTF-16 little-endian"),
        new(TextFileEncodingKind.Windows1252, "Windows-1252"),
    ];

    public IReadOnlyList<ChoiceOption<LineEndingKind>> LineEndingChoices { get; } =
    [
        new(LineEndingKind.CrLf, "Windows (CRLF)"),
        new(LineEndingKind.Lf, "Unix (LF)"),
    ];

    public IReadOnlyList<ChoiceOption<FileCollisionPolicy?>> FileCollisionChoices { get; } =
    [
        new(null, "Use template default"),
        new(FileCollisionPolicy.Skip, "Skip"),
        new(FileCollisionPolicy.Overwrite, "Overwrite"),
        new(FileCollisionPolicy.Fail, "Fail"),
    ];

    public IReadOnlyList<FolderRoleChoice> FolderRoleChoices { get; } =
    [
        new(null, "No semantic role"),
        new(ProjectPathRoles.CaptureRaw, "Raw capture destination"),
        new(ProjectPathRoles.CaptureProcessed, "Processed capture destination"),
    ];

    public IReadOnlyList<VariableOption> VariableChoices { get; } =
    [
        new("{{project_name}}", "Project name", "Name of the target folder"),
        new("{{project_path}}", "Project path", "Absolute target-folder path"),
        new("{{project_id}}", "Project ID", "UUIDv7 assigned to the project"),
        new("{{created_date}}", "Created date", "UTC date in yyyy-MM-dd format"),
        new("{{created_at_utc}}", "Created timestamp", "Full UTC creation timestamp"),
        new("{{user_name}}", "Windows user name", "Current Windows account name"),
    ];

    public RelayCommand NewTemplateCommand { get; }

    public AsyncRelayCommand DuplicateTemplateCommand { get; }

    public AsyncRelayCommand SaveTemplateCommand { get; }

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand AddTextFileCommand { get; }

    public RelayCommand DeleteNodeCommand { get; }

    public RelayCommand RenameNodeCommand { get; }

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
                OnPropertyChanged(nameof(CanUpdateProjectStructure));
                NotifyCommandStates();
                StatusMessage = value?.HasError == true
                    ? value.ErrorMessage ?? "This template could not be loaded."
                    : value?.Editor?.ValidationStatus ?? "No template selected";
            }
        }
    }

    public TemplateEditorViewModel? SelectedTemplate => SelectedListItem?.Editor;

    public bool HasSelectedTemplate => SelectedTemplate is not null;

    public bool CanUpdateProjectStructure =>
        SelectedTemplate is { IsNew: false, IsDirty: false, IsValid: true };

    public EditorNodeViewModel? SelectedNode
    {
        get => selectedNode;
        set
        {
            EditorNodeViewModel? previous = selectedNode;
            if (SetProperty(ref selectedNode, value))
            {
                if (previous is not null)
                {
                    previous.Changed -= SelectedNodeChanged;
                }

                if (value is not null)
                {
                    value.Changed += SelectedNodeChanged;
                }

                OnPropertyChanged(nameof(HasSelectedNode));
                OnPropertyChanged(nameof(IsProjectRootSelected));
                RefreshFolderRoleChoices();
                NotifyCommandStates();
            }
        }
    }

    public bool HasSelectedNode => SelectedNode is not null;

    public bool IsProjectRootSelected => SelectedNode is null;

    public bool CanMoveNode(EditorNodeViewModel node, EditorNodeViewModel? newParent)
    {
        if (SelectedTemplate is null || (newParent is not null && newParent.Kind != EditorNodeKind.Folder))
        {
            return false;
        }

        for (EditorNodeViewModel? ancestor = newParent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, node))
            {
                return false;
            }
        }

        return Flatten(SelectedTemplate.Nodes).Contains(node);
    }

    public bool MoveNode(EditorNodeViewModel node, EditorNodeViewModel? newParent, int insertionIndex)
    {
        TemplateEditorViewModel? template = SelectedTemplate;
        if (template is null || !CanMoveNode(node, newParent))
        {
            return false;
        }

        ObservableCollection<EditorNodeViewModel> oldSiblings = node.Parent?.Children ?? template.Nodes;
        ObservableCollection<EditorNodeViewModel> newSiblings = newParent?.Children ?? template.Nodes;
        int oldIndex = oldSiblings.IndexOf(node);
        int targetIndex = Math.Clamp(insertionIndex, 0, newSiblings.Count);
        if (ReferenceEquals(oldSiblings, newSiblings) && oldIndex < targetIndex)
        {
            targetIndex--;
        }

        if (ReferenceEquals(oldSiblings, newSiblings) && oldIndex == targetIndex)
        {
            return false;
        }

        oldSiblings.RemoveAt(oldIndex);
        node.SetParent(newParent);
        newSiblings.Insert(targetIndex, node);
        SortCollection(template.Nodes);
        template.NotifyStructureChanged();
        SelectedNode = node;
        return true;
    }

    public async Task AttachFileAsync(string sourcePath)
    {
        if (SelectedTemplate is not TemplateEditorViewModel template)
        {
            return;
        }

        Guid nodeId = Guid.CreateVersion7();
        try
        {
            StagedAsset staged = await store.StageAssetAsync(template.TemplateId, nodeId, sourcePath).ConfigureAwait(true);
            EditorNodeViewModel asset = EditorNodeViewModel.CreateAssetFile(
                nodeId,
                staged.OriginalFileName,
                staged.RelativePath,
                staged.Sha256);
            EditorNodeViewModel? parent = GetInsertionParent();
            if (parent is null)
            {
                template.AddRoot(asset);
            }
            else
            {
                parent.AddChild(asset);
            }

            SortCollection(template.Nodes);
            template.NotifyStructureChanged();
            StatusMessage = $"Attached '{staged.OriginalFileName}'. Save the template to commit it to the package.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not attach the file: {exception.Message}";
        }
    }

    public async Task ReplaceSelectedAssetAsync(string sourcePath)
    {
        if (SelectedTemplate is not TemplateEditorViewModel template || SelectedNode is not { IsAssetFile: true } asset)
        {
            return;
        }

        try
        {
            StagedAsset staged = await store.StageAssetAsync(template.TemplateId, asset.NodeId, sourcePath).ConfigureAwait(true);
            asset.ReplaceAsset(staged.RelativePath, staged.Sha256);
            StatusMessage = $"Replaced the attached contents from '{staged.OriginalFileName}'. The project filename remains '{asset.Name}'.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not replace the attached file: {exception.Message}";
        }
    }

    public ResolvedPreviewResult BuildResolvedPreview(string targetPath)
    {
        if (SelectedTemplate is not TemplateEditorViewModel template)
        {
            return new ResolvedPreviewResult(targetPath, string.Empty, [], ["No template is selected."]);
        }

        TemplateDocument document = template.Snapshot();
        List<string> errors = [];
        ContractValidationResult assetValidation = store.ValidateEditingAssets(document);
        errors.AddRange(assetValidation.Issues.Select(issue => issue.Message));

        GenerationContext context;
        GenerationPlan plan;
        try
        {
            context = GenerationContext.CreateNew(targetPath);
            plan = GenerationPlanBuilder.Build(document, context);
        }
        catch (GenerationPlanException exception)
        {
            errors.AddRange(exception.Issues.Select(issue => issue.Message));
            return new ResolvedPreviewResult(targetPath, string.Empty, [], errors.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            errors.Add(exception.Message);
            return new ResolvedPreviewResult(targetPath, string.Empty, [], errors.Distinct(StringComparer.Ordinal).ToArray());
        }

        List<ResolvedPreviewItem> items = [];
        foreach (GenerationOperation operation in plan.Operations)
        {
            string kind;
            string outcome;
            string content = string.Empty;
            switch (operation)
            {
                case CreateDirectoryOperation:
                    kind = "Folder";
                    outcome = File.Exists(operation.FullPath)
                        ? "Blocked: a file already uses this path."
                        : Directory.Exists(operation.FullPath)
                            ? "Existing folder will be reused."
                            : "Create folder.";
                    if (File.Exists(operation.FullPath))
                    {
                        errors.Add($"A file already occupies the folder output '{operation.RelativePath}'.");
                    }

                    break;
                case CreateTextFileOperation textFile:
                    kind = "Editable text file";
                    content = textFile.Content;
                    outcome = GetFilePreviewOutcome(textFile, errors);
                    break;
                case CopyAssetFileOperation assetFile:
                    kind = "Attached file";
                    outcome = GetFilePreviewOutcome(assetFile, errors);
                    break;
                default:
                    continue;
            }

            items.Add(new ResolvedPreviewItem(
                operation is CreateDirectoryOperation ? "□" : operation is CreateTextFileOperation ? "▤" : "◇",
                operation.RelativePath,
                kind,
                outcome,
                content));
        }

        items.Add(new ResolvedPreviewItem(
            "◆",
            ".projectID.json",
            "Hidden project identity",
            "Created after successful output generation, or reused for a matching existing project.",
            string.Empty));
        string variableSummary = string.Join(Environment.NewLine, context.ToVariableValues().Select(pair => $"{{{{{pair.Key}}}}}  =  {pair.Value}"));
        return new ResolvedPreviewResult(
            context.ProjectPath,
            variableSummary,
            items,
            errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    public void DiscardAllStaging()
    {
        foreach (Guid templateId in Templates.Select(item => item.TemplateId).Distinct())
        {
            store.DiscardStaging(templateId);
        }
    }

    private static string GetFilePreviewOutcome(CreateFileOperation file, List<string> errors)
    {
        if (Directory.Exists(file.FullPath))
        {
            errors.Add($"A folder already occupies the file output '{file.RelativePath}'.");
            return "Blocked: a folder already uses this path.";
        }

        if (!File.Exists(file.FullPath))
        {
            return "Create file.";
        }

        return file.Collision switch
        {
            FileCollisionPolicy.Skip => "Existing file will be kept (Skip).",
            FileCollisionPolicy.Overwrite => "Existing file will be replaced (Overwrite).",
            FileCollisionPolicy.Fail => AddCollisionError(file.RelativePath, errors),
            _ => "Existing file requires collision handling.",
        };
    }

    private static string AddCollisionError(string relativePath, List<string> errors)
    {
        errors.Add($"The existing file '{relativePath}' blocks generation because its collision policy is Fail.");
        return "Blocked by existing file (Fail).";
    }

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
        string initialStatus = Templates.Count == 0 ? "Create your first template." : SelectedTemplate?.ValidationStatus ?? "A damaged template is selected.";
        StatusMessage = SynchronizeExplorerMenu() ?? initialStatus;
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
            StatusMessage = SynchronizeExplorerMenu() ?? "Template saved.";
            NotifyCommandStates();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ContractValidationException)
        {
            StatusMessage = $"Save failed: {exception.Message}";
            return false;
        }
    }

    public async Task<ProjectStructureUpdateResult?> UpdateSelectedProjectStructureAsync(
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!CanUpdateProjectStructure || SelectedTemplate is null)
        {
            StatusMessage = "Save a valid template before updating project structure metadata.";
            return null;
        }

        try
        {
            ProjectStructureUpdateResult result = await ProjectStructureUpdater.UpdateAsync(
                new ProjectStructureUpdateRequest
                {
                    Template = SelectedTemplate.Snapshot(),
                    TargetDirectory = targetDirectory,
                },
                cancellationToken).ConfigureAwait(true);
            StatusMessage = result.Created
                ? "Project structure metadata created."
                : "Project structure metadata updated.";
            return result;
        }
        catch (ProjectStructureUpdateException exception)
        {
            string detail = exception.Issues.Count > 0 ? exception.Issues[0].Message : exception.Message;
            StatusMessage = $"Metadata update failed: {detail}";
            return null;
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

        StatusMessage = SynchronizeExplorerMenu() ?? "All templates saved.";
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
            StatusMessage = SynchronizeExplorerMenu() ?? StatusMessage;
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

    private async Task DuplicateTemplateAsync()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        TemplateEditorViewModel source = SelectedTemplate;
        TemplateEditorViewModel duplicate = TemplateEditorViewModel.Duplicate(source);
        try
        {
            await store.StageAssetsForDuplicateAsync(source.TemplateId, duplicate.Snapshot()).ConfigureAwait(true);
            AddEditor(duplicate);
            StatusMessage = "Duplicated as a new unsaved template, including its attached files.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            store.DiscardStaging(duplicate.TemplateId);
            StatusMessage = $"Could not duplicate the template assets: {exception.Message}";
        }
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
        EditorNodeViewModel? parent = GetInsertionParent();
        if (parent is not null)
        {
            parent.AddChild(folder);
        }
        else
        {
            template.AddRoot(folder);
        }

        SortCollection(template.Nodes);
        template.NotifyStructureChanged();
    }

    private void AddTextFile()
    {
        TemplateEditorViewModel template = SelectedTemplate!;
        EditorNodeViewModel file = EditorNodeViewModel.CreateTextFile(GetUniqueName("New File"));
        EditorNodeViewModel? parent = GetInsertionParent();
        if (parent is not null)
        {
            parent.AddChild(file);
        }
        else
        {
            template.AddRoot(file);
        }

        SortCollection(template.Nodes);
        template.NotifyStructureChanged();
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

    private void RenameNode() => SelectedNode?.BeginRename();

    public void NormalizeStructureOrder()
    {
        if (SelectedTemplate is not TemplateEditorViewModel template)
        {
            return;
        }

        SortCollection(template.Nodes);
        template.NotifyStructureChanged();
        NotifyCommandStates();
    }

    private static void SortCollection(ObservableCollection<EditorNodeViewModel> nodes)
    {
        foreach (EditorNodeViewModel folder in nodes.Where(node => node.Kind == EditorNodeKind.Folder))
        {
            SortCollection(folder.Children);
        }

        EditorNodeViewModel[] ordered = nodes
            .OrderBy(node => node.Kind == EditorNodeKind.Folder ? 0 : 1)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.Ordinal)
            .ToArray();
        for (int targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
        {
            int currentIndex = nodes.IndexOf(ordered[targetIndex]);
            if (currentIndex != targetIndex)
            {
                nodes.Move(currentIndex, targetIndex);
            }
        }
    }

    private string GetUniqueName(string baseName)
    {
        EditorNodeViewModel? parent = GetInsertionParent();
        IEnumerable<string> names = (parent?.Children ?? SelectedTemplate!.Nodes).Select(node => node.Name);
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

    private EditorNodeViewModel? GetInsertionParent() =>
        SelectedNode?.Kind == EditorNodeKind.Folder ? SelectedNode : SelectedNode?.Parent;

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
            StatusMessage = SynchronizeExplorerMenu() ?? (item.IsExplorerEnabled
                ? "Template added to the Explorer menu."
                : "Template removed from the Explorer menu.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not update Explorer state: {exception.Message}";
        }
    }

    private string? SynchronizeExplorerMenu()
    {
        try
        {
            ExplorerMenuTemplate[] enabled = Templates
                .Where(item => item.IsExplorerEnabled && item.Editor is { IsNew: false })
                .Select(item => new ExplorerMenuTemplate(item.TemplateId, item.Editor!.Name))
                .ToArray();
            _ = explorerMenu.Synchronize(enabled);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return $"Explorer menu could not be updated: {exception.Message}";
        }
    }

    private void SelectedTemplatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is TemplateEditorViewModel template && template == SelectedTemplate &&
            e.PropertyName is nameof(TemplateEditorViewModel.IsDirty) or nameof(TemplateEditorViewModel.IsValid))
        {
            OnPropertyChanged(nameof(CanUpdateProjectStructure));
            NotifyCommandStates();
            StatusMessage = template.ValidationStatus;
        }
    }

    private void SelectedNodeChanged(object? sender, EventArgs e) => RefreshFolderRoleChoices();

    private void RefreshFolderRoleChoices()
    {
        foreach (FolderRoleChoice choice in FolderRoleChoices)
        {
            if (choice.Value is null)
            {
                choice.SetAvailability(true);
                continue;
            }

            EditorNodeViewModel? owner = SelectedTemplate is null
                ? null
                : Flatten(SelectedTemplate.Nodes).FirstOrDefault(node =>
                    node.IsFolder && string.Equals(node.Role, choice.Value, StringComparison.Ordinal));
            bool available = owner is null || ReferenceEquals(owner, SelectedNode);
            choice.SetAvailability(available, available || owner is null ? null : GetNodePath(owner));
        }
    }

    private static string GetNodePath(EditorNodeViewModel node)
    {
        Stack<string> segments = [];
        for (EditorNodeViewModel? current = node; current is not null; current = current.Parent)
        {
            segments.Push(current.Name);
        }

        return string.Join('/', segments);
    }

    private void NotifyCommandStates()
    {
        DuplicateTemplateCommand.NotifyCanExecuteChanged();
        SaveTemplateCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        AddTextFileCommand.NotifyCanExecuteChanged();
        DeleteNodeCommand.NotifyCanExecuteChanged();
        RenameNodeCommand.NotifyCanExecuteChanged();
    }

    private static IEnumerable<EditorNodeViewModel> Flatten(IEnumerable<EditorNodeViewModel> nodes)
    {
        foreach (EditorNodeViewModel node in nodes)
        {
            yield return node;
            foreach (EditorNodeViewModel child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
