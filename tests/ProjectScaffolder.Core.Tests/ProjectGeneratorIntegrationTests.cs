using System.Security.Cryptography;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Serialization;

namespace ProjectScaffolder.Core.Tests;

public sealed class ProjectGeneratorIntegrationTests
{
    [Fact]
    public async Task GeneratesCompleteProjectAndHiddenIdentity()
    {
        using TestWorkspace workspace = new();
        byte[] assetBytes = [0, 1, 2, 3, 254, 255];
        TemplateDocument template = workspace.CreateTemplate(assetBytes);

        GenerationResult result = await ProjectGenerator.GenerateAsync(workspace.CreateRequest(template));

        Assert.True(result.CreatedProjectIdentity);
        Assert.Equal(2, result.CreatedDirectoryCount);
        Assert.Equal(2, result.CreatedFileCount);
        Assert.Equal("Project: Hotel Lobby\r\nPath: " + workspace.TargetDirectory + "\r\n", await File.ReadAllTextAsync(Path.Combine(workspace.TargetDirectory, "README.txt")));
        Assert.Equal(assetBytes, await File.ReadAllBytesAsync(Path.Combine(workspace.TargetDirectory, "ASSETS", "reference.bin")));

        string identityPath = Path.Combine(workspace.TargetDirectory, ProjectGenerator.ProjectIdentityFileName);
        Assert.True(File.GetAttributes(identityPath).HasFlag(FileAttributes.Hidden));
        ProjectIdentityDocument identity = ValidatedContractReader.ReadProjectIdentity(await File.ReadAllTextAsync(identityPath));
        Assert.Equal(result.ProjectId, identity.ProjectId);
        Assert.Equal(template.TemplateId, identity.Template.Id);
        Assert.Equal("Hotel Lobby", identity.ProjectName);
    }

    [Fact]
    public async Task RerunPreservesIdentityAndSkipsExistingFiles()
    {
        using TestWorkspace workspace = new();
        TemplateDocument template = workspace.CreateTemplate([10, 20, 30]);
        GenerationRequest request = workspace.CreateRequest(template);
        GenerationResult first = await ProjectGenerator.GenerateAsync(request);
        string textPath = Path.Combine(workspace.TargetDirectory, "README.txt");
        await File.WriteAllTextAsync(textPath, "artist edit");

        GenerationResult second = await ProjectGenerator.GenerateAsync(request);

        Assert.False(second.CreatedProjectIdentity);
        Assert.Equal(first.ProjectId, second.ProjectId);
        Assert.Equal(2, second.SkippedFileCount);
        Assert.Equal("artist edit", await File.ReadAllTextAsync(textPath));
    }

    [Fact]
    public async Task DifferentTemplateCannotModifyExistingProject()
    {
        using TestWorkspace workspace = new();
        TemplateDocument original = workspace.CreateTemplate([1]);
        await ProjectGenerator.GenerateAsync(workspace.CreateRequest(original));
        string markerPath = Path.Combine(workspace.TargetDirectory, "artist-work.txt");
        await File.WriteAllTextAsync(markerPath, "keep");
        TemplateDocument different = workspace.CreateTemplate([2], Guid.CreateVersion7());

        ProjectGenerationException exception = await Assert.ThrowsAsync<ProjectGenerationException>(
            () => ProjectGenerator.GenerateAsync(workspace.CreateRequest(different)));

        Assert.Contains("different template", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", await File.ReadAllTextAsync(markerPath));
        Assert.False(File.Exists(Path.Combine(workspace.TargetDirectory, "unexpected.txt")));
    }

    [Fact]
    public async Task OverwritePolicyReplacesExistingFile()
    {
        using TestWorkspace workspace = new();
        TemplateDocument template = workspace.CreateTemplate([4, 5, 6], textCollision: FileCollisionPolicy.Overwrite);
        string textPath = Path.Combine(workspace.TargetDirectory, "README.txt");
        await File.WriteAllTextAsync(textPath, "old");

        GenerationResult result = await ProjectGenerator.GenerateAsync(workspace.CreateRequest(template));

        Assert.Equal(1, result.OverwrittenFileCount);
        Assert.StartsWith("Project: Hotel Lobby", await File.ReadAllTextAsync(textPath), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(workspace.TargetDirectory, "*.bak", SearchOption.AllDirectories));
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.GeneratorTests", Guid.NewGuid().ToString("N"));

        public TestWorkspace()
        {
            TargetDirectory = Path.Combine(rootDirectory, "Hotel Lobby");
            Directory.CreateDirectory(TargetDirectory);
        }

        public string TargetDirectory { get; }

        public TemplateDocument CreateTemplate(
            byte[] assetBytes,
            Guid? templateId = null,
            FileCollisionPolicy? textCollision = null)
        {
            Guid id = templateId ?? Guid.CreateVersion7();
            string templateDirectory = Path.Combine(rootDirectory, "templates", id.ToString("D"));
            string assetsDirectory = Path.Combine(templateDirectory, "assets");
            Directory.CreateDirectory(assetsDirectory);
            File.WriteAllBytes(Path.Combine(assetsDirectory, "reference.bin"), assetBytes);
            string digest = Convert.ToHexString(SHA256.HashData(assetBytes)).ToLowerInvariant();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new TemplateDocument
            {
                SchemaVersion = 1,
                TemplateId = id,
                Name = "Integration Template",
                TemplateVersion = 1,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                DefaultFileCollision = FileCollisionPolicy.Skip,
                Nodes =
                [
                    new FolderNode
                    {
                        NodeId = Guid.CreateVersion7(),
                        Name = "SCENES",
                        Children = [],
                    },
                    new FolderNode
                    {
                        NodeId = Guid.CreateVersion7(),
                        Name = "ASSETS",
                        Children =
                        [
                            new AssetFileNode
                            {
                                NodeId = Guid.CreateVersion7(),
                                Name = "reference.bin",
                                Asset = new AssetReference { Path = "assets/reference.bin", Sha256 = digest },
                            },
                        ],
                    },
                    new TextFileNode
                    {
                        NodeId = Guid.CreateVersion7(),
                        Name = "README.txt",
                        Collision = textCollision,
                        Encoding = TextFileEncodingKind.Utf8,
                        LineEndings = LineEndingKind.CrLf,
                        Content = "Project: {{project_name}}\nPath: {{project_path}}\n",
                    },
                ],
            };
        }

        public GenerationRequest CreateRequest(TemplateDocument template) => new()
        {
            Template = template,
            TemplateDirectory = Path.Combine(rootDirectory, "templates", template.TemplateId.ToString("D")),
            TargetDirectory = TargetDirectory,
            GeneratorVersion = "integration-test",
            UserName = "Test Artist",
        };

        public void Dispose()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }
    }
}
