using System.Collections.ObjectModel;
using System.IO;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Validation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Editor.ViewModels;

internal sealed class TemplateEditorViewModel : ObservableObject
{
    private string name;
    private string? description;
    private FileCollisionPolicy defaultFileCollision;
    private int templateVersion;
    private DateTimeOffset modifiedAtUtc;
    private bool isDirty;
    private bool isNew;
    private bool isValid;
    private string validationSummary = string.Empty;

    public TemplateEditorViewModel(TemplateDocument document, bool isNew = false, bool createNewNodeIds = false)
    {
        TemplateId = isNew ? Guid.CreateVersion7() : document.TemplateId;
        name = document.Name;
        description = document.Description;
        defaultFileCollision = document.DefaultFileCollision;
        templateVersion = isNew ? 1 : document.TemplateVersion;
        CreatedAtUtc = isNew ? DateTimeOffset.UtcNow : document.CreatedAtUtc;
        modifiedAtUtc = isNew ? CreatedAtUtc : document.ModifiedAtUtc;
        this.isNew = isNew;
        foreach (TemplateNode node in document.Nodes)
        {
            AddRoot(EditorNodeViewModel.FromContract(node, createNewNodeIds));
        }

        Nodes.CollectionChanged += (_, _) => MarkDirtyAndValidate();
        if (isNew)
        {
            isDirty = true;
        }

        Validate();
    }

    public Guid TemplateId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public ObservableCollection<EditorNodeViewModel> Nodes { get; } = [];

    public IReadOnlyList<FileCollisionPolicy> CollisionPolicies { get; } = Enum.GetValues<FileCollisionPolicy>();

    public string Name
    {
        get => name;
        set
        {
            if (SetProperty(ref name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                MarkDirtyAndValidate();
            }
        }
    }

    public string DisplayName => IsDirty ? $"{Name} *" : Name;

    public string? Description
    {
        get => description;
        set
        {
            if (SetProperty(ref description, value))
            {
                MarkDirtyAndValidate();
            }
        }
    }

    public FileCollisionPolicy DefaultFileCollision
    {
        get => defaultFileCollision;
        set
        {
            if (SetProperty(ref defaultFileCollision, value))
            {
                MarkDirtyAndValidate();
            }
        }
    }

    public int TemplateVersion
    {
        get => templateVersion;
        private set => SetProperty(ref templateVersion, value);
    }

    public DateTimeOffset ModifiedAtUtc
    {
        get => modifiedAtUtc;
        private set => SetProperty(ref modifiedAtUtc, value);
    }

    public bool IsDirty
    {
        get => isDirty;
        private set
        {
            if (SetProperty(ref isDirty, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DirtyStatus));
            }
        }
    }

    public bool IsNew
    {
        get => isNew;
        private set => SetProperty(ref isNew, value);
    }

    public bool IsValid
    {
        get => isValid;
        private set
        {
            if (SetProperty(ref isValid, value))
            {
                OnPropertyChanged(nameof(ValidationStatus));
            }
        }
    }

    public string ValidationSummary
    {
        get => validationSummary;
        private set => SetProperty(ref validationSummary, value);
    }

    public string ValidationStatus => IsValid ? "Valid template" : "Template has errors";

    public string DirtyStatus => IsDirty ? "Unsaved changes" : "All changes saved";

    public int FolderCount => Flatten(Nodes).Count(node => node.Kind == EditorNodeKind.Folder);

    public int FileCount => Flatten(Nodes).Count(node => node.IsFile);

    public string ItemSummary => $"{FolderCount} folders · {FileCount} files";

    public void AddRoot(EditorNodeViewModel node)
    {
        Nodes.Add(node);
        node.Changed += NodeChanged;
    }

    public void NotifyStructureChanged()
    {
        OnPropertyChanged(nameof(FolderCount));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(ItemSummary));
        MarkDirtyAndValidate();
    }

    public TemplateDocument CreateSaveDocument()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int version = IsNew ? 1 : TemplateVersion + 1;
        return BuildDocument(version, now);
    }

    public void AcceptSaved(TemplateDocument document)
    {
        TemplateVersion = document.TemplateVersion;
        ModifiedAtUtc = document.ModifiedAtUtc;
        IsNew = false;
        IsDirty = false;
        Validate();
    }

    public TemplateDocument Snapshot() => BuildDocument(TemplateVersion, ModifiedAtUtc);

    public static TemplateEditorViewModel CreateNew() => new(new TemplateDocument
    {
        SchemaVersion = 2,
        TemplateId = Guid.CreateVersion7(),
        Name = "New Template",
        Description = null,
        TemplateVersion = 1,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        ModifiedAtUtc = DateTimeOffset.UtcNow,
        DefaultFileCollision = FileCollisionPolicy.Skip,
        Nodes = [],
    }, true);

    public static TemplateEditorViewModel Duplicate(TemplateEditorViewModel source) =>
        new(source.Snapshot() with { Name = source.Name + " Copy" }, true, true);

    private TemplateDocument BuildDocument(int version, DateTimeOffset modified) => new()
    {
        SchemaVersion = 2,
        TemplateId = TemplateId,
        Name = Name,
        Description = Description,
        TemplateVersion = version,
        CreatedAtUtc = CreatedAtUtc,
        ModifiedAtUtc = modified,
        DefaultFileCollision = DefaultFileCollision,
        Nodes = Nodes.Select(node => node.ToContract()).ToArray(),
    };

    private void NodeChanged(object? sender, EventArgs e) => NotifyStructureChanged();

    private void MarkDirtyAndValidate()
    {
        IsDirty = true;
        Validate();
    }

    private void Validate()
    {
        ContractValidationResult result = TemplateValidator.Validate(Snapshot());
        List<ValidationIssue> issues = result.Issues.ToList();
        if (result.IsValid)
        {
            try
            {
                string samplePath = Path.Combine(Path.GetTempPath(), "ProjectScaffolder Preview", "Sample Project");
                _ = GenerationPlanBuilder.Build(Snapshot(), GenerationContext.CreateNew(samplePath, "Sample Artist"));
            }
            catch (GenerationPlanException exception)
            {
                issues.AddRange(exception.Issues);
            }
        }

        IsValid = issues.Count == 0;
        ValidationSummary = IsValid
            ? "No validation errors."
            : string.Join(Environment.NewLine, issues.Select(issue => $"• {issue.Message}"));
    }

    private static IEnumerable<EditorNodeViewModel> Flatten(IEnumerable<EditorNodeViewModel> nodes)
    {
        foreach (EditorNodeViewModel node in nodes)
        {
            yield return node;
            foreach (EditorNodeViewModel descendant in Flatten(node.Children))
            {
                yield return descendant;
            }
        }
    }
}
