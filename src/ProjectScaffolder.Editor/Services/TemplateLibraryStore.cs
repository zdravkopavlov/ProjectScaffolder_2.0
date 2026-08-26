using System.IO;
using System.Security.Cryptography;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Paths;
using ProjectScaffolder.Core.Persistence;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Editor.Services;

internal sealed record StoredTemplate(Guid TemplateId, TemplateDocument? Document, string? ErrorMessage);

internal sealed record LoadedTemplateLibrary(
    IReadOnlyList<StoredTemplate> Templates,
    IReadOnlySet<Guid> ExplorerTemplateIds);

internal sealed record StagedAsset(string RelativePath, string Sha256, string OriginalFileName);

internal sealed class TemplateLibraryStore(EditorPaths paths)
{
    private TemplateLibraryDocument library = new()
    {
        SchemaVersion = 1,
        TemplateOrder = [],
        ExplorerTemplateIds = [],
    };

    public async Task<LoadedTemplateLibrary> LoadAsync(CancellationToken cancellationToken = default)
    {
        ResetStagingArea();
        Directory.CreateDirectory(paths.TemplatesRoot);
        Directory.CreateDirectory(paths.SettingsRoot);
        await SeedBundledTemplatesAsync(cancellationToken).ConfigureAwait(false);

        if (File.Exists(paths.LibraryPath))
        {
            library = ValidatedContractReader.ReadTemplateLibrary(
                await File.ReadAllTextAsync(paths.LibraryPath, cancellationToken).ConfigureAwait(false));
        }

        Dictionary<Guid, StoredTemplate> discovered = [];
        foreach (string directory in Directory.EnumerateDirectories(paths.TemplatesRoot))
        {
            if (!Guid.TryParseExact(Path.GetFileName(directory), "D", out Guid id))
            {
                continue;
            }

            string templatePath = Path.Combine(directory, "template.json");
            try
            {
                TemplateDocument document = ValidatedContractReader.ReadTemplate(
                    await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false));
                if (document.TemplateId != id)
                {
                    throw new ContractValidationException("The template ID does not match its package directory.", []);
                }

                discovered[id] = new StoredTemplate(id, document, null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ContractValidationException)
            {
                discovered[id] = new StoredTemplate(id, null, exception.Message);
            }
        }

        List<Guid> order = library.TemplateOrder.Where(discovered.ContainsKey).ToList();
        order.AddRange(discovered.Keys.Where(id => !order.Contains(id)).OrderBy(id => id));
        library = library with
        {
            TemplateOrder = order,
            ExplorerTemplateIds = library.ExplorerTemplateIds.Where(discovered.ContainsKey).ToArray(),
        };
        await SaveLibraryAsync(cancellationToken).ConfigureAwait(false);
        return new LoadedTemplateLibrary(
            order.Select(id => discovered[id]).ToArray(),
            library.ExplorerTemplateIds.ToHashSet());
    }

    public async Task SaveTemplateAsync(TemplateDocument document, CancellationToken cancellationToken = default)
    {
        ContractValidationResult validation = TemplateValidator.Validate(document);
        if (!validation.IsValid)
        {
            throw new ContractValidationException("Only valid templates can be saved.", validation.Issues);
        }

        string templateDirectory = Path.Combine(paths.TemplatesRoot, document.TemplateId.ToString("D"));
        HashSet<string> previousAssetPaths = ReadSavedAssetPaths(templateDirectory);
        await CommitStagedAssetsAsync(document, templateDirectory, cancellationToken).ConfigureAwait(false);
        ContractValidationResult assets = TemplateAssetValidator.Validate(document, templateDirectory);
        if (!assets.IsValid)
        {
            throw new ContractValidationException("The template contains missing or changed attached files.", assets.Issues);
        }

        string templatePath = Path.Combine(templateDirectory, "template.json");
        await AtomicJsonFile.SaveAsync(templatePath, ContractJson.Serialize(document), true, cancellationToken).ConfigureAwait(false);
        try
        {
            DeleteAssetsNoLongerReferenced(templateDirectory, previousAssetPaths, FlattenAssets(document.Nodes).Select(asset => asset.Asset.Path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The template commit succeeded. An obsolete managed asset may remain and can be retried on a later save.
        }
        if (!library.TemplateOrder.Contains(document.TemplateId))
        {
            library = library with { TemplateOrder = library.TemplateOrder.Append(document.TemplateId).ToArray() };
            await SaveLibraryAsync(cancellationToken).ConfigureAwait(false);
        }

        DiscardStaging(document.TemplateId);
    }

    public async Task<StagedAsset> StageAssetAsync(
        Guid templateId,
        Guid nodeId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("The file to attach does not exist.", fullSourcePath);
        }

        string stagingDirectory = GetStagingDirectory(templateId);
        Directory.CreateDirectory(stagingDirectory);
        string temporaryPath = Path.Combine(stagingDirectory, $"attach-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream source = File.Open(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (FileStream destination = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            string digest;
            await using (FileStream staged = File.Open(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                digest = Convert.ToHexString(await SHA256.HashDataAsync(staged, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            }
            string originalName = Path.GetFileName(fullSourcePath);
            string storedName = SanitizeAssetFileName(originalName);
            string relativePath = $"assets/{nodeId:N}/{digest[..12]}-{storedName}";
            string finalPath = ResolveStagingPath(templateId, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            File.Move(temporaryPath, finalPath, true);
            return new StagedAsset(relativePath, digest, originalName);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task StageAssetsForDuplicateAsync(
        Guid sourceTemplateId,
        TemplateDocument duplicate,
        CancellationToken cancellationToken = default)
    {
        foreach (AssetFileNode asset in FlattenAssets(duplicate.Nodes))
        {
            string sourcePath = ResolveAvailableAssetPath(sourceTemplateId, asset.Asset.Path);
            string destinationPath = ResolveStagingPath(duplicate.TemplateId, asset.Asset.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await CopyFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        }
    }

    public ContractValidationResult ValidateEditingAssets(TemplateDocument document)
    {
        List<ValidationIssue> issues = [];
        foreach (AssetFileNode asset in FlattenAssets(document.Nodes))
        {
            try
            {
                string fullPath = ResolveAvailableAssetPath(document.TemplateId, asset.Asset.Path);
                using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                string digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                if (!digest.Equals(asset.Asset.Sha256, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue("asset.hash", asset.Asset.Path, $"Attached file '{asset.Name}' no longer matches its recorded SHA-256 digest."));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                issues.Add(new ValidationIssue("asset.missing", asset.Asset.Path, $"Attached file '{asset.Name}' is unavailable: {exception.Message}"));
            }
        }

        return new ContractValidationResult(issues);
    }

    public async Task<string> CreateDisposablePackageAsync(
        TemplateDocument document,
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(packageRoot);
        Directory.CreateDirectory(root);
        string templateDirectory = Path.Combine(root, document.TemplateId.ToString("D"));
        Directory.CreateDirectory(templateDirectory);
        foreach (AssetFileNode asset in FlattenAssets(document.Nodes))
        {
            string sourcePath = ResolveAvailableAssetPath(document.TemplateId, asset.Asset.Path);
            string destinationPath = PathSafety.ResolveDescendant(templateDirectory, asset.Asset.Path.Split('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await CopyFileAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        }

        ContractValidationResult assets = TemplateAssetValidator.Validate(document, templateDirectory);
        if (!assets.IsValid)
        {
            throw new ContractValidationException("The disposable template package contains invalid attached files.", assets.Issues);
        }

        await AtomicJsonFile.SaveAsync(
            Path.Combine(templateDirectory, "template.json"),
            ContractJson.Serialize(document),
            false,
            cancellationToken).ConfigureAwait(false);
        return templateDirectory;
    }

    public void DiscardStaging(Guid templateId)
    {
        string directory = GetStagingDirectory(templateId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    public async Task SetExplorerEnabledAsync(Guid templateId, bool enabled, CancellationToken cancellationToken = default)
    {
        HashSet<Guid> enabledIds = library.ExplorerTemplateIds.ToHashSet();
        if (enabled)
        {
            enabledIds.Add(templateId);
        }
        else
        {
            enabledIds.Remove(templateId);
        }

        library = library with
        {
            ExplorerTemplateIds = library.TemplateOrder.Where(enabledIds.Contains).ToArray(),
        };
        await SaveLibraryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> RemoveTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        DiscardStaging(templateId);
        string sourceDirectory = Path.Combine(paths.TemplatesRoot, templateId.ToString("D"));
        string? trashDirectory = null;
        if (Directory.Exists(sourceDirectory))
        {
            Directory.CreateDirectory(paths.TrashRoot);
            trashDirectory = Path.Combine(
                paths.TrashRoot,
                $"{templateId:D}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");
            Directory.Move(sourceDirectory, trashDirectory);
        }

        TemplateLibraryDocument previousLibrary = library;
        library = library with
        {
            TemplateOrder = library.TemplateOrder.Where(id => id != templateId).ToArray(),
            ExplorerTemplateIds = library.ExplorerTemplateIds.Where(id => id != templateId).ToArray(),
        };

        try
        {
            await SaveLibraryAsync(cancellationToken).ConfigureAwait(false);
            return trashDirectory;
        }
        catch
        {
            library = previousLibrary;
            if (trashDirectory is not null && Directory.Exists(trashDirectory) && !Directory.Exists(sourceDirectory))
            {
                Directory.Move(trashDirectory, sourceDirectory);
            }

            throw;
        }
    }

    private async Task SaveLibraryAsync(CancellationToken cancellationToken) =>
        await AtomicJsonFile.SaveAsync(paths.LibraryPath, ContractJson.Serialize(library), false, cancellationToken).ConfigureAwait(false);

    private async Task SeedBundledTemplatesAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(paths.SeedMarkerPath))
        {
            return;
        }

        string seedRoot = Path.Combine(AppContext.BaseDirectory, "SeedTemplates");
        if (!Directory.Exists(seedRoot))
        {
            return;
        }

        foreach (string seedPath in Directory.EnumerateFiles(seedRoot, "*.json"))
        {
            TemplateDocument seed = ValidatedContractReader.ReadTemplate(
                await File.ReadAllTextAsync(seedPath, cancellationToken).ConfigureAwait(false));
            string destinationDirectory = Path.Combine(paths.TemplatesRoot, seed.TemplateId.ToString("D"));
            string destinationPath = Path.Combine(destinationDirectory, "template.json");
            if (!File.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationDirectory);
                await AtomicJsonFile.SaveAsync(destinationPath, ContractJson.Serialize(seed), false, cancellationToken).ConfigureAwait(false);
            }
        }

        await AtomicJsonFile.SaveAsync(paths.SeedMarkerPath, "seeded" + Environment.NewLine, false, cancellationToken).ConfigureAwait(false);
    }

    private async Task CommitStagedAssetsAsync(
        TemplateDocument document,
        string templateDirectory,
        CancellationToken cancellationToken)
    {
        foreach (AssetFileNode asset in FlattenAssets(document.Nodes))
        {
            string stagedPath = ResolveStagingPath(document.TemplateId, asset.Asset.Path);
            if (!File.Exists(stagedPath))
            {
                continue;
            }

            string destinationPath = PathSafety.ResolveDescendant(templateDirectory, asset.Asset.Path.Split('/'));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            string temporaryPath = destinationPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                await CopyFileAsync(stagedPath, temporaryPath, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, destinationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private string ResolveAvailableAssetPath(Guid templateId, string relativePath)
    {
        string stagedPath = ResolveStagingPath(templateId, relativePath);
        if (File.Exists(stagedPath))
        {
            return stagedPath;
        }

        string templateDirectory = Path.Combine(paths.TemplatesRoot, templateId.ToString("D"));
        string packagePath = PathSafety.ResolveDescendant(templateDirectory, relativePath.Split('/'));
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("An attached file could not be found while duplicating the template.", packagePath);
        }

        return packagePath;
    }

    private string ResolveStagingPath(Guid templateId, string relativePath) =>
        PathSafety.ResolveDescendant(GetStagingDirectory(templateId), relativePath.Split('/'));

    private string GetStagingDirectory(Guid templateId) => Path.Combine(paths.StagingRoot, templateId.ToString("D"));

    private void ResetStagingArea()
    {
        if (Directory.Exists(paths.StagingRoot))
        {
            Directory.Delete(paths.StagingRoot, true);
        }

        Directory.CreateDirectory(paths.StagingRoot);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using FileStream destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<AssetFileNode> FlattenAssets(IEnumerable<TemplateNode> nodes)
    {
        foreach (TemplateNode node in nodes)
        {
            if (node is AssetFileNode asset)
            {
                yield return asset;
            }
            else if (node is FolderNode folder)
            {
                foreach (AssetFileNode descendant in FlattenAssets(folder.Children))
                {
                    yield return descendant;
                }
            }
        }
    }

    private static HashSet<string> ReadSavedAssetPaths(string templateDirectory)
    {
        string templatePath = Path.Combine(templateDirectory, "template.json");
        if (!File.Exists(templatePath))
        {
            return [];
        }

        try
        {
            TemplateDocument saved = ValidatedContractReader.ReadTemplate(File.ReadAllText(templatePath));
            return FlattenAssets(saved.Nodes).Select(asset => asset.Asset.Path).ToHashSet(StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ContractValidationException)
        {
            return [];
        }
    }

    private static void DeleteAssetsNoLongerReferenced(
        string templateDirectory,
        IEnumerable<string> previousPaths,
        IEnumerable<string> currentPaths)
    {
        HashSet<string> retained = currentPaths.ToHashSet(StringComparer.Ordinal);
        string assetsRoot = Path.Combine(templateDirectory, "assets");
        foreach (string relativePath in previousPaths.Where(path => !retained.Contains(path)))
        {
            string fullPath = PathSafety.ResolveDescendant(templateDirectory, relativePath.Split('/'));
            if (File.Exists(fullPath) && !File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
            {
                File.Delete(fullPath);
            }

            string? directory = Path.GetDirectoryName(fullPath);
            while (directory is not null &&
                   !directory.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                   Directory.Exists(directory) &&
                   !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }

        if (Directory.Exists(assetsRoot) && !Directory.EnumerateFileSystemEntries(assetsRoot).Any())
        {
            Directory.Delete(assetsRoot);
        }
    }

    private static string SanitizeAssetFileName(string fileName)
    {
        string sanitized = string.Concat(fileName.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '_'));
        sanitized = sanitized.Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "attached-file";
        }

        return sanitized.Length <= 120 ? sanitized : sanitized[..120];
    }
}
