using System.Text;
using System.Text.Json;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Paths;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Core.Validation;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Generation;

public static class ProjectGenerator
{
    public const string ProjectIdentityFileName = ".projectID.json";
    public const string ProjectStructureFileName = ".projectStructure.json";

    public static async Task<GenerationResult> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        string templateDirectory = Path.GetFullPath(request.TemplateDirectory);
        string targetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.TargetDirectory));
        ValidateTemplatePackage(request.Template, templateDirectory);
        ValidateTargetDirectory(targetDirectory);

        string identityPath = Path.Combine(targetDirectory, ProjectIdentityFileName);
        ProjectIdentityDocument? existingIdentity = await ReadExistingIdentityAsync(
            identityPath,
            request.Template.TemplateId,
            cancellationToken).ConfigureAwait(false);
        string structurePath = Path.Combine(targetDirectory, ProjectStructureFileName);
        ProjectStructureDocument? existingStructure = await ReadExistingStructureAsync(
            structurePath,
            existingIdentity?.ProjectId,
            cancellationToken).ConfigureAwait(false);

        GenerationContext context = existingIdentity is null
            ? GenerationContext.CreateNew(targetDirectory, request.UserName)
            : GenerationContext.CreateExisting(targetDirectory, existingIdentity.ProjectId, existingIdentity.CreatedAtUtc, request.UserName);

        GenerationPlan plan;
        try
        {
            plan = GenerationPlanBuilder.Build(request.Template, context);
        }
        catch (GenerationPlanException exception)
        {
            throw new ProjectGenerationException(exception.Message, exception.Issues, innerException: exception);
        }

        GenerationPreflightResult preflight = GenerationPreflight.Inspect(plan, new PhysicalFileSystemProbe());
        if (!preflight.IsValid)
        {
            throw new ProjectGenerationException("The target folder contains blocking conflicts.", preflight.Issues);
        }

        OperationJournal journal = new();
        int createdDirectories = 0;
        int reusedDirectories = 0;
        int createdFiles = 0;
        int skippedFiles = 0;
        int overwrittenFiles = 0;

        try
        {
            foreach (PreflightOperation item in preflight.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (item.Operation)
                {
                    case CreateDirectoryOperation directory:
                        if (item.Disposition == PreflightDisposition.Create)
                        {
                            Directory.CreateDirectory(directory.FullPath);
                            journal.RecordCreatedDirectory(directory.FullPath);
                            if (directory.Hidden)
                            {
                                File.SetAttributes(
                                    directory.FullPath,
                                    File.GetAttributes(directory.FullPath) | FileAttributes.Hidden);
                            }

                            createdDirectories++;
                        }
                        else
                        {
                            reusedDirectories++;
                        }

                        break;
                    case CreateTextFileOperation textFile:
                        if (item.Disposition == PreflightDisposition.SkipExistingFile)
                        {
                            skippedFiles++;
                            break;
                        }

                        byte[] bytes = TextFileEncoder.Encode(textFile.Content, textFile.Encoding, textFile.LineEndings);
                        await TransactionalFileWriter.WriteBytesAsync(
                            textFile.FullPath,
                            bytes,
                            item.Disposition,
                            journal,
                            cancellationToken).ConfigureAwait(false);
                        CountWrittenFile(item.Disposition, ref createdFiles, ref overwrittenFiles);
                        break;
                    case CopyAssetFileOperation assetFile:
                        if (item.Disposition == PreflightDisposition.SkipExistingFile)
                        {
                            skippedFiles++;
                            break;
                        }

                        string sourcePath = ResolveAssetPath(templateDirectory, assetFile.AssetPath);
                        await TransactionalFileWriter.CopyAssetAsync(
                            sourcePath,
                            assetFile.FullPath,
                            assetFile.Sha256,
                            item.Disposition,
                            journal,
                            cancellationToken).ConfigureAwait(false);
                        CountWrittenFile(item.Disposition, ref createdFiles, ref overwrittenFiles);
                        break;
                }
            }

            bool createdIdentity = existingIdentity is null;
            if (createdIdentity)
            {
                ProjectIdentityDocument identity = ProjectIdentityFactory.Create(request.Template, context, request.GeneratorVersion);
                byte[] identityBytes = Encoding.UTF8.GetBytes(ContractJson.Serialize(identity));
                await TransactionalFileWriter.WriteBytesAsync(
                    identityPath,
                    identityBytes,
                    PreflightDisposition.Create,
                    journal,
                    cancellationToken).ConfigureAwait(false);
                File.SetAttributes(identityPath, File.GetAttributes(identityPath) | FileAttributes.Hidden);
                ProjectIdentityDocument persistedIdentity = ValidatedContractReader.ReadProjectIdentity(
                    await File.ReadAllTextAsync(identityPath, cancellationToken).ConfigureAwait(false));
                if (persistedIdentity.ProjectId != context.ProjectId)
                {
                    throw new IOException("The project identity file did not retain the generated project ID.");
                }
            }

            bool writeStructure = plan.SemanticPaths.Count > 0 || existingStructure is not null;
            bool createdStructure = writeStructure && existingStructure is null;
            if (writeStructure)
            {
                var structure = new ProjectStructureDocument
                {
                    SchemaVersion = 1,
                    ProjectId = context.ProjectId,
                    Paths = plan.SemanticPaths,
                };
                ContractValidationResult validation = ProjectStructureValidator.Validate(structure);
                if (!validation.IsValid)
                {
                    throw new ContractValidationException("The resolved project structure is invalid.", validation.Issues);
                }

                byte[] structureBytes = Encoding.UTF8.GetBytes(ContractJson.Serialize(structure));
                await TransactionalFileWriter.WriteBytesAsync(
                    structurePath,
                    structureBytes,
                    createdStructure ? PreflightDisposition.Create : PreflightDisposition.OverwriteExistingFile,
                    journal,
                    cancellationToken).ConfigureAwait(false);
                File.SetAttributes(structurePath, File.GetAttributes(structurePath) | FileAttributes.Hidden);
                ProjectStructureDocument persistedStructure = ValidatedContractReader.ReadProjectStructure(
                    await File.ReadAllTextAsync(structurePath, cancellationToken).ConfigureAwait(false));
                if (persistedStructure.ProjectId != context.ProjectId)
                {
                    throw new IOException("The project structure file did not retain the project ID.");
                }
            }

            journal.Commit();
            return new GenerationResult
            {
                ProjectId = context.ProjectId,
                CreatedProjectIdentity = createdIdentity,
                CreatedProjectStructure = createdStructure,
                CreatedDirectoryCount = createdDirectories,
                ReusedDirectoryCount = reusedDirectories,
                CreatedFileCount = createdFiles,
                SkippedFileCount = skippedFiles,
                OverwrittenFileCount = overwrittenFiles,
            };
        }
        catch (Exception exception)
        {
            RollbackResult rollback = journal.Rollback();
            throw new ProjectGenerationException(
                "Project generation failed while applying the validated plan.",
                rollback: rollback,
                innerException: exception);
        }
    }

    private static void ValidateRequest(GenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GeneratorVersion) || request.GeneratorVersion.Length > 64)
        {
            throw new ProjectGenerationException("The generator version must contain between 1 and 64 characters.");
        }
    }

    private static void ValidateTemplatePackage(TemplateDocument template, string templateDirectory)
    {
        ContractValidationResult contract = TemplateValidator.Validate(template);
        if (!contract.IsValid)
        {
            throw new ProjectGenerationException("The selected template violates its supported contract.", contract.Issues);
        }

        string directoryName = new DirectoryInfo(templateDirectory).Name;
        if (!directoryName.Equals(template.TemplateId.ToString("D"), StringComparison.Ordinal))
        {
            throw new ProjectGenerationException(
                "The template directory name does not match its template ID.",
                [new ValidationIssue("template.packageDirectory", "$.templateDirectory", "The template package directory must use the canonical template ID.")]);
        }

        ContractValidationResult assets = TemplateAssetValidator.Validate(template, templateDirectory);
        if (!assets.IsValid)
        {
            throw new ProjectGenerationException("The selected template contains invalid attached assets.", assets.Issues);
        }
    }

    private static void ValidateTargetDirectory(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            throw TargetConflict("target.missing", "The target folder does not exist.");
        }

        FileAttributes attributes = File.GetAttributes(targetDirectory);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw TargetConflict("target.reparsePoint", "The target folder is a symbolic link or reparse point and cannot be scaffolded safely.");
        }

        string identityPath = Path.Combine(targetDirectory, ProjectIdentityFileName);
        if (Directory.Exists(identityPath))
        {
            throw TargetConflict("target.identityPath", "A directory occupies the reserved .projectID.json path.");
        }

        string structurePath = Path.Combine(targetDirectory, ProjectStructureFileName);
        if (Directory.Exists(structurePath))
        {
            throw TargetConflict("target.structurePath", "A directory occupies the reserved .projectStructure.json path.");
        }
    }

    private static async Task<ProjectIdentityDocument?> ReadExistingIdentityAsync(
        string identityPath,
        Guid selectedTemplateId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(identityPath))
        {
            return null;
        }

        if (File.GetAttributes(identityPath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw TargetConflict("target.identityReparsePoint", "The existing .projectID.json is a symbolic link or reparse point.");
        }

        try
        {
            string json = await File.ReadAllTextAsync(identityPath, cancellationToken).ConfigureAwait(false);
            ProjectIdentityDocument identity = ValidatedContractReader.ReadProjectIdentity(json);
            if (identity.Template.Id != selectedTemplateId)
            {
                throw TargetConflict("target.differentTemplate", "This folder already belongs to a project created from a different template.");
            }

            return identity;
        }
        catch (ProjectGenerationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ContractValidationException)
        {
            throw new ProjectGenerationException(
                "The existing .projectID.json is unreadable or invalid and must be repaired explicitly.",
                [new ValidationIssue("target.invalidIdentity", "$.target", "The existing .projectID.json must be repaired explicitly before generation can continue.")],
                innerException: exception);
        }
    }

    private static async Task<ProjectStructureDocument?> ReadExistingStructureAsync(
        string structurePath,
        Guid? expectedProjectId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(structurePath))
        {
            return null;
        }

        if (expectedProjectId is null)
        {
            throw TargetConflict("target.orphanStructure", "The target contains .projectStructure.json without a valid .projectID.json.");
        }

        if (File.GetAttributes(structurePath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw TargetConflict("target.structureReparsePoint", "The existing .projectStructure.json is a symbolic link or reparse point.");
        }

        try
        {
            ProjectStructureDocument structure = ValidatedContractReader.ReadProjectStructure(
                await File.ReadAllTextAsync(structurePath, cancellationToken).ConfigureAwait(false));
            if (structure.ProjectId != expectedProjectId.Value)
            {
                throw TargetConflict("target.structureProjectMismatch", "The existing .projectStructure.json belongs to a different project ID.");
            }

            return structure;
        }
        catch (ProjectGenerationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ContractValidationException)
        {
            throw new ProjectGenerationException(
                "The existing .projectStructure.json is unreadable or invalid and must be repaired explicitly.",
                [new ValidationIssue("target.invalidStructure", "$.target", "The existing .projectStructure.json must be repaired explicitly before generation can continue.")],
                innerException: exception);
        }
    }

    private static ProjectGenerationException TargetConflict(string code, string message) =>
        new(message, [new ValidationIssue(code, "$.target", message)]);

    private static string ResolveAssetPath(string templateDirectory, string assetPath) =>
        PathSafety.ResolveDescendant(templateDirectory, assetPath.Split('/'));

    private static void CountWrittenFile(PreflightDisposition disposition, ref int createdFiles, ref int overwrittenFiles)
    {
        if (disposition == PreflightDisposition.Create)
        {
            createdFiles++;
        }
        else
        {
            overwrittenFiles++;
        }
    }
}
