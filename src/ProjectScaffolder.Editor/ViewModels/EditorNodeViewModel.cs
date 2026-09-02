using System.Collections.ObjectModel;
using System.IO;
using ProjectScaffolder.Core.Contracts;

namespace ProjectScaffolder.Editor.ViewModels;

internal enum EditorNodeKind
{
    Folder,
    TextFile,
    AssetFile,
}

internal sealed class EditorNodeViewModel : ObservableObject
{
    private string name;
    private FileCollisionPolicy? collision;
    private TextFileEncodingKind encoding;
    private LineEndingKind lineEndings;
    private string content;
    private string? assetPath;
    private string? assetSha256;
    private bool isEditing;
    private bool isDropTarget;
    private bool isHidden;
    private string? role;

    private EditorNodeViewModel(
        Guid nodeId,
        EditorNodeKind kind,
        string name,
        FileCollisionPolicy? collision,
        TextFileEncodingKind encoding,
        LineEndingKind lineEndings,
        string content,
        string? assetPath,
        string? assetSha256,
        bool isHidden = false,
        string? role = null)
    {
        NodeId = nodeId;
        Kind = kind;
        this.name = name;
        this.collision = collision;
        this.encoding = encoding;
        this.lineEndings = lineEndings;
        this.content = content;
        this.assetPath = assetPath;
        this.assetSha256 = assetSha256;
        this.isHidden = isHidden;
        this.role = role;
        Children.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public Guid NodeId { get; }

    public EditorNodeKind Kind { get; }

    public EditorNodeViewModel? Parent { get; private set; }

    public ObservableCollection<EditorNodeViewModel> Children { get; } = [];

    public string Name
    {
        get => name;
        set
        {
            if (SetProperty(ref name, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public FileCollisionPolicy? Collision
    {
        get => collision;
        set
        {
            if (SetProperty(ref collision, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public TextFileEncodingKind Encoding
    {
        get => encoding;
        set
        {
            if (SetProperty(ref encoding, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public LineEndingKind LineEndings
    {
        get => lineEndings;
        set
        {
            if (SetProperty(ref lineEndings, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Content
    {
        get => content;
        set
        {
            if (SetProperty(ref content, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string? AssetPath => assetPath;

    public string? AssetSha256 => assetSha256;

    public string AssetFileName => assetPath is null ? string.Empty : Path.GetFileName(assetPath.Replace('/', Path.DirectorySeparatorChar));

    public string AssetDigestDisplay => assetSha256 is null ? string.Empty : $"SHA-256  {assetSha256}";

    public string TypeLabel => Kind switch
    {
        EditorNodeKind.Folder => "Folder",
        EditorNodeKind.TextFile => "Editable text file",
        EditorNodeKind.AssetFile => "Attached file",
        _ => "Unknown",
    };

    public string Icon => Kind switch
    {
        EditorNodeKind.Folder => "□",
        EditorNodeKind.TextFile => "▤",
        EditorNodeKind.AssetFile => "◇",
        _ => "•",
    };

    public bool IsTextFile => Kind == EditorNodeKind.TextFile;

    public bool IsAssetFile => Kind == EditorNodeKind.AssetFile;

    public bool IsFile => Kind != EditorNodeKind.Folder;

    public bool IsFolder => Kind == EditorNodeKind.Folder;

    public bool IsHidden
    {
        get => isHidden;
        set
        {
            if (SetProperty(ref isHidden, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string? Role
    {
        get => role;
        set
        {
            if (SetProperty(ref role, value))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsEditing
    {
        get => isEditing;
        set => SetProperty(ref isEditing, value);
    }

    public bool IsDropTarget
    {
        get => isDropTarget;
        set => SetProperty(ref isDropTarget, value);
    }

    public void BeginRename() => IsEditing = true;

    public void EndRename() => IsEditing = false;

    public void ReplaceAsset(string path, string sha256)
    {
        assetPath = path;
        assetSha256 = sha256;
        OnPropertyChanged(nameof(AssetPath));
        OnPropertyChanged(nameof(AssetSha256));
        OnPropertyChanged(nameof(AssetFileName));
        OnPropertyChanged(nameof(AssetDigestDisplay));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddChild(EditorNodeViewModel child)
    {
        child.SetParent(this);
        Children.Add(child);
        child.Changed += ChildChanged;
    }

    internal void SetParent(EditorNodeViewModel? parent) => Parent = parent;

    public TemplateNode ToContract() => Kind switch
    {
        EditorNodeKind.Folder => new FolderNode
        {
            NodeId = NodeId,
            Name = Name,
            Hidden = IsHidden,
            Role = Role,
            Children = Children.Select(child => child.ToContract()).ToArray(),
        },
        EditorNodeKind.TextFile => new TextFileNode
        {
            NodeId = NodeId,
            Name = Name,
            Collision = Collision,
            Encoding = Encoding,
            LineEndings = LineEndings,
            Content = Content,
        },
        EditorNodeKind.AssetFile => new AssetFileNode
        {
            NodeId = NodeId,
            Name = Name,
            Collision = Collision,
            Asset = new AssetReference
            {
                Path = AssetPath ?? string.Empty,
                Sha256 = AssetSha256 ?? string.Empty,
            },
        },
        _ => throw new InvalidOperationException("Unsupported editor node kind."),
    };

    public static EditorNodeViewModel FromContract(TemplateNode node, bool createNewIds = false)
    {
        Guid id = createNewIds ? Guid.CreateVersion7() : node.NodeId;
        EditorNodeViewModel result = node switch
        {
            FolderNode folderNode => new EditorNodeViewModel(id, EditorNodeKind.Folder, node.Name, null, TextFileEncodingKind.Utf8, LineEndingKind.CrLf, string.Empty, null, null, folderNode.Hidden, folderNode.Role),
            TextFileNode text => new EditorNodeViewModel(id, EditorNodeKind.TextFile, node.Name, text.Collision, text.Encoding, text.LineEndings, text.Content, null, null),
            AssetFileNode asset => new EditorNodeViewModel(id, EditorNodeKind.AssetFile, node.Name, asset.Collision, TextFileEncodingKind.Utf8, LineEndingKind.CrLf, string.Empty, asset.Asset.Path, asset.Asset.Sha256),
            _ => throw new NotSupportedException($"Unsupported template node type '{node.GetType().Name}'."),
        };

        if (node is FolderNode folder)
        {
            foreach (TemplateNode child in folder.Children)
            {
                result.AddChild(FromContract(child, createNewIds));
            }
        }

        return result;
    }

    public static EditorNodeViewModel CreateFolder(string name) =>
        new(Guid.CreateVersion7(), EditorNodeKind.Folder, name, null, TextFileEncodingKind.Utf8, LineEndingKind.CrLf, string.Empty, null, null);

    public static EditorNodeViewModel CreateTextFile(string name) =>
        new(Guid.CreateVersion7(), EditorNodeKind.TextFile, name, null, TextFileEncodingKind.Utf8, LineEndingKind.CrLf, string.Empty, null, null);

    public static EditorNodeViewModel CreateAssetFile(Guid nodeId, string name, string assetPath, string assetSha256) =>
        new(nodeId, EditorNodeKind.AssetFile, name, null, TextFileEncodingKind.Utf8, LineEndingKind.CrLf, string.Empty, assetPath, assetSha256);

    private void ChildChanged(object? sender, EventArgs e) => Changed?.Invoke(sender, e);
}
