using System.Text.Json;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Serialization;

namespace ProjectScaffolder.Runner.Tests;

public sealed class RunnerFailureIntegrationTests
{
    [Fact]
    public async Task ReadOnlyOverwriteLogsFullWrappedExceptionAndRollsBackCreatedFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "Scaffolder.RunnerTests", Guid.NewGuid().ToString("N"));
        string target = Path.Combine(root, "target");
        string logs = Path.Combine(root, "logs");
        Guid id = Guid.CreateVersion7();
        string templates = Path.Combine(root, "templates");
        string package = Path.Combine(templates, id.ToString("D"));
        string existing = Path.Combine(target, "keep.txt");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(package);
        try
        {
            await File.WriteAllTextAsync(existing, "original");
            File.SetAttributes(existing, FileAttributes.ReadOnly);
            var template = new TemplateDocument
            {
                SchemaVersion = 1, TemplateId = id, Name = "Failure test", TemplateVersion = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow, ModifiedAtUtc = DateTimeOffset.UtcNow,
                DefaultFileCollision = FileCollisionPolicy.Skip,
                Nodes =
                [
                    new FolderNode { NodeId = Guid.CreateVersion7(), Name = "created-before-failure", Children = [] },
                    new TextFileNode
                    {
                        NodeId = Guid.CreateVersion7(), Name = "keep.txt", Content = "replacement",
                        Collision = FileCollisionPolicy.Overwrite,
                        Encoding = TextFileEncodingKind.Utf8, LineEndings = LineEndingKind.CrLf,
                    },
                ],
            };
            await File.WriteAllTextAsync(Path.Combine(package, "template.json"), ContractJson.Serialize(template));
            int exitCode = await RunnerApplication.RunAsync(
                ["--template-id", id.ToString("D"), "--target", target,
                 "--templates-root", templates, "--logs-root", logs, "--no-ui"]);

            Assert.Equal((int)RunnerExitCode.GenerationFailed, exitCode);
            using JsonDocument log = JsonDocument.Parse(await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(logs, "*.json"))));
            JsonElement entry = log.RootElement;
            Assert.Equal("failure", entry.GetProperty("outcome").GetString());
            Assert.True(entry.GetProperty("rollbackCompleted").GetBoolean());
            Assert.Empty(entry.GetProperty("pathsRequiringInspection").EnumerateArray());
            Assert.Equal("ProjectScaffolder.Core.Generation.ProjectGenerationException", entry.GetProperty("errorType").GetString());
            Assert.Equal("Project generation failed while applying the validated plan.", entry.GetProperty("errorMessage").GetString());
            string details = entry.GetProperty("errorDetails").GetString()!;
            Assert.Contains("System.UnauthorizedAccessException", details, StringComparison.Ordinal);
            Assert.Contains("ProjectGenerator.GenerateAsync", details, StringComparison.Ordinal);
            Assert.Contains("RunnerApplication.RunAsync", details, StringComparison.Ordinal);
            Assert.Equal("original", await File.ReadAllTextAsync(existing));
            Assert.False(Directory.Exists(Path.Combine(target, "created-before-failure")));
            Assert.Equal(existing, Assert.Single(Directory.GetFiles(target)));
        }
        finally
        {
            if (File.Exists(existing)) File.SetAttributes(existing, FileAttributes.Normal);
            Directory.Delete(root, true);
        }
    }
}
