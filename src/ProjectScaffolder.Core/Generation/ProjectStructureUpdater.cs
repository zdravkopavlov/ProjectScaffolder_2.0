using System.Text;
using System.Text.Json;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Paths;
using ProjectScaffolder.Core.Persistence;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Core.Validation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Generation;

public static class ProjectStructureUpdater
{
    private const int MaximumMetadataBytes = 64 * 1024;

    public static async Task<ProjectStructureUpdateResult> UpdateAsync(
        ProjectStructureUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string targetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.TargetDirectory));
        ValidateTemplate(request.Template);
        ValidateTarget(targetDirectory);

        string identityPath = Path.Combine(targetDirectory, ProjectGenerator.ProjectIdentityFileName);
        ProjectIdentityDocument identity = await ReadIdentityAsync(identityPath, cancellationToken).ConfigureAwait(false);
        if (identity.Template.Id != request.Template.TemplateId)
        {
            throw Failure(
                "template.mismatch",
                "The selected template does not match the template recorded by this project identity.");
        }

        string structurePath = Path.Combine(targetDirectory, ProjectGenerator.ProjectStructureFileName);
        ProjectStructureDocument? existing = await ReadExistingStructureAsync(
            structurePath,
            identity.ProjectId,
            cancellationToken).ConfigureAwait(false);

        GenerationContext context = new()
        {
            ProjectName = identity.ProjectName,
            ProjectPath = targetDirectory,
            ProjectId = identity.ProjectId,
            CreatedAtUtc = identity.CreatedAtUtc,
            UserName = request.UserName ?? Environment.UserName,
        };

        GenerationPlan plan;
        try
        {
            plan = GenerationPlanBuilder.Build(request.Template, context);
        }
        catch (GenerationPlanException exception)
        {
            throw new ProjectStructureUpdateException(
                "The selected template roles could not be resolved for this project.",
                exception.Issues,
                exception);
        }

        if (plan.SemanticPaths.Count == 0)
        {
            throw Failure("role.none", "The selected template does not declare any semantic folder roles.");
        }

        ValidateDeclaredDirectories(targetDirectory, plan.SemanticPaths);
        var structure = new ProjectStructureDocument
        {
            SchemaVersion = 1,
            ProjectId = identity.ProjectId,
            Paths = plan.SemanticPaths,
        };
        ContractValidationResult validation = ProjectStructureValidator.Validate(structure);
        if (!validation.IsValid)
        {
            throw new ProjectStructureUpdateException(
                "The resolved project structure is invalid.",
                validation.Issues);
        }

        try
        {
            await AtomicJsonFile.SaveAsync(
                structurePath,
                ContractJson.Serialize(structure),
                keepBackup: false,
                cancellationToken).ConfigureAwait(false);
            File.SetAttributes(structurePath, File.GetAttributes(structurePath) | FileAttributes.Hidden);
            ProjectStructureDocument persisted = ValidatedContractReader.ReadProjectStructure(
                await ReadBoundedTextAsync(structurePath, cancellationToken).ConfigureAwait(false));
            if (persisted.ProjectId != identity.ProjectId ||
                !persisted.Paths.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .SequenceEqual(plan.SemanticPaths.OrderBy(pair => pair.Key, StringComparer.Ordinal)))
            {
                throw new IOException("The persisted project structure did not retain the resolved semantic paths.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ContractValidationException)
        {
            throw new ProjectStructureUpdateException(
                "The project structure metadata could not be written and verified.",
                innerException: exception);
        }

        return new ProjectStructureUpdateResult
        {
            ProjectId = identity.ProjectId,
            Created = existing is null,
            Paths = plan.SemanticPaths,
        };
    }

    private static void ValidateTemplate(TemplateDocument template)
    {
        ContractValidationResult validation = TemplateValidator.Validate(template);
        if (!validation.IsValid)
        {
            throw new ProjectStructureUpdateException(
                "The selected template violates its supported contract.",
                validation.Issues);
        }
    }

    private static void ValidateTarget(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            throw Failure("target.missing", "The selected project folder does not exist.");
        }

        if (File.GetAttributes(targetDirectory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw Failure("target.reparsePoint", "The selected project folder is a symbolic link or reparse point.");
        }

        foreach (string fileName in new[] { ProjectGenerator.ProjectIdentityFileName, ProjectGenerator.ProjectStructureFileName })
        {
            string path = Path.Combine(targetDirectory, fileName);
            if (Directory.Exists(path))
            {
                throw Failure("target.metadataPath", $"A directory occupies the reserved {fileName} path.");
            }

            if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            {
                throw Failure("target.metadataReparsePoint", $"The existing {fileName} is a symbolic link or reparse point.");
            }
        }
    }

    private static async Task<ProjectIdentityDocument> ReadIdentityAsync(
        string identityPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(identityPath))
        {
            throw Failure("identity.missing", "The selected folder does not contain .projectID.json.");
        }

        try
        {
            return ValidatedContractReader.ReadProjectIdentity(
                await ReadBoundedTextAsync(identityPath, cancellationToken).ConfigureAwait(false));
        }
        catch (ProjectStructureUpdateException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ContractValidationException)
        {
            throw new ProjectStructureUpdateException(
                "The existing .projectID.json is unreadable or invalid.",
                innerException: exception);
        }
    }

    private static async Task<ProjectStructureDocument?> ReadExistingStructureAsync(
        string structurePath,
        Guid expectedProjectId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(structurePath))
        {
            return null;
        }

        try
        {
            ProjectStructureDocument structure = ValidatedContractReader.ReadProjectStructure(
                await ReadBoundedTextAsync(structurePath, cancellationToken).ConfigureAwait(false));
            if (structure.ProjectId != expectedProjectId)
            {
                throw Failure(
                    "structure.projectMismatch",
                    "The existing .projectStructure.json belongs to a different Project ID.");
            }

            return structure;
        }
        catch (ProjectStructureUpdateException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ContractValidationException)
        {
            throw new ProjectStructureUpdateException(
                "The existing .projectStructure.json is unreadable or invalid and was not replaced.",
                innerException: exception);
        }
    }

    private static void ValidateDeclaredDirectories(
        string targetDirectory,
        IReadOnlyDictionary<string, string> semanticPaths)
    {
        foreach ((string role, string relativePath) in semanticPaths)
        {
            string current = targetDirectory;
            foreach (string segment in relativePath.Split('/'))
            {
                current = PathSafety.ResolveDescendant(current, [segment]);
                if (!Directory.Exists(current))
                {
                    throw Failure(
                        "role.directoryMissing",
                        $"The folder declared for '{role}' does not exist: {relativePath}.");
                }

                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                {
                    throw Failure(
                        "role.directoryReparsePoint",
                        $"The folder declared for '{role}' traverses a symbolic link or reparse point: {relativePath}.");
                }
            }
        }
    }

    private static async Task<string> ReadBoundedTextAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumMetadataBytes)
        {
            throw new IOException($"Metadata file '{Path.GetFileName(path)}' exceeds {MaximumMetadataBytes} bytes.");
        }

        using StreamReader reader = new(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ProjectStructureUpdateException Failure(string code, string message) =>
        new(message, [new ValidationIssue(code, "$.target", message)]);
}
