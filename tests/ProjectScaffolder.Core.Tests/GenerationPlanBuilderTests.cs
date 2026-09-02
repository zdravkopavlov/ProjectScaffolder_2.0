using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Tests;

public sealed class GenerationPlanBuilderTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildsResolvedPlanWithoutWritingFiles()
    {
        TemplateDocument template = CreateTemplate([
            new FolderNode
            {
                NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(1)),
                Name = "SCENES",
                Hidden = true,
                Children = [],
            },
            new TextFileNode
            {
                NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(2)),
                Name = "{{project_name}}.mxp",
                Encoding = TextFileEncodingKind.Utf8,
                LineEndings = LineEndingKind.CrLf,
                Content = "ProjectFolder={{project_path}}",
            },
        ]);
        string targetPath = Path.Combine(Path.GetTempPath(), "Project Scaffolder Tests", "Hotel Lobby");
        GenerationContext context = GenerationContext.CreateNew(targetPath, "Artist", Timestamp);

        GenerationPlan plan = GenerationPlanBuilder.Build(template, context);

        Assert.Collection(
            plan.Operations,
            directory =>
            {
                CreateDirectoryOperation folder = Assert.IsType<CreateDirectoryOperation>(directory);
                Assert.Equal("SCENES", folder.RelativePath);
                Assert.True(folder.Hidden);
            },
            file =>
            {
                CreateTextFileOperation textFile = Assert.IsType<CreateTextFileOperation>(file);
                Assert.Equal("Hotel Lobby.mxp", textFile.RelativePath);
                Assert.Equal($"ProjectFolder={context.ProjectPath}", textFile.Content);
                Assert.Equal(FileCollisionPolicy.Skip, textFile.Collision);
            });
    }

    [Fact]
    public void RejectsCaseInsensitiveDuplicateOutputs()
    {
        TemplateDocument template = CreateTemplate([
            CreateFolder("SCENES", 1),
            CreateFolder("scenes", 2),
        ]);
        GenerationContext context = GenerationContext.CreateNew(Path.Combine(Path.GetTempPath(), "Duplicate Test"), "Artist", Timestamp);

        GenerationPlanException exception = Assert.Throws<GenerationPlanException>(() => GenerationPlanBuilder.Build(template, context));

        Assert.Contains(exception.Issues, issue => issue.Code == "path.duplicate");
    }

    [Fact]
    public void ResolvesSemanticPathFromCompleteFolderAncestry()
    {
        TemplateDocument template = CreateTemplate(
        [
            new FolderNode
            {
                NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(1)),
                Name = "DATASET",
                Children =
                [
                    new FolderNode
                    {
                        NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(2)),
                        Name = "{{user_name}}",
                        Role = ProjectPathRoles.CaptureRaw,
                        Children = [],
                    },
                    new FolderNode
                    {
                        NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(3)),
                        Name = "processed",
                        Role = ProjectPathRoles.CaptureProcessed,
                        Children = [],
                    },
                ],
            },
        ]) with
        { SchemaVersion = 2 };
        GenerationContext context = GenerationContext.CreateNew(
            Path.Combine(Path.GetTempPath(), "Semantic Path Test"),
            "Artist",
            Timestamp);

        GenerationPlan plan = GenerationPlanBuilder.Build(template, context);

        Assert.Equal("DATASET/Artist", plan.SemanticPaths[ProjectPathRoles.CaptureRaw]);
        Assert.Equal("DATASET/processed", plan.SemanticPaths[ProjectPathRoles.CaptureProcessed]);
    }

    [Fact]
    public void RejectsReservedProjectIdentityOutput()
    {
        TemplateDocument template = CreateTemplate([
            new TextFileNode
            {
                NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(1)),
                Name = ".projectID.json",
                Encoding = TextFileEncodingKind.Utf8,
                LineEndings = LineEndingKind.CrLf,
                Content = string.Empty,
            },
        ]);
        GenerationContext context = GenerationContext.CreateNew(Path.Combine(Path.GetTempPath(), "Reserved Test"), "Artist", Timestamp);

        GenerationPlanException exception = Assert.Throws<GenerationPlanException>(() => GenerationPlanBuilder.Build(template, context));

        Assert.Contains(exception.Issues, issue => issue.Code == "name.reserved");
    }

    private static FolderNode CreateFolder(string name, int milliseconds) => new()
    {
        NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(milliseconds)),
        Name = name,
        Children = [],
    };

    private static TemplateDocument CreateTemplate(IReadOnlyList<TemplateNode> nodes) => new()
    {
        SchemaVersion = 1,
        TemplateId = Guid.CreateVersion7(Timestamp),
        Name = "Test Template",
        TemplateVersion = 1,
        CreatedAtUtc = Timestamp,
        ModifiedAtUtc = Timestamp,
        DefaultFileCollision = FileCollisionPolicy.Skip,
        Nodes = nodes,
    };
}
