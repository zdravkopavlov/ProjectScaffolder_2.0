using System.IO;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Persistence;
using ProjectScaffolder.Core.Serialization;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Editor.Services;

internal sealed record StoredTemplate(Guid TemplateId, TemplateDocument? Document, string? ErrorMessage);

internal sealed record LoadedTemplateLibrary(
    IReadOnlyList<StoredTemplate> Templates,
    IReadOnlySet<Guid> ExplorerTemplateIds);

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
        ContractValidationResult assets = TemplateAssetValidator.Validate(document, templateDirectory);
        if (!assets.IsValid)
        {
            throw new ContractValidationException("The template contains missing or changed attached files.", assets.Issues);
        }

        string templatePath = Path.Combine(templateDirectory, "template.json");
        await AtomicJsonFile.SaveAsync(templatePath, ContractJson.Serialize(document), true, cancellationToken).ConfigureAwait(false);
        if (!library.TemplateOrder.Contains(document.TemplateId))
        {
            library = library with { TemplateOrder = library.TemplateOrder.Append(document.TemplateId).ToArray() };
            await SaveLibraryAsync(cancellationToken).ConfigureAwait(false);
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
}
