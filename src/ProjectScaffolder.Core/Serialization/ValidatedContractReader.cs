using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Serialization;

public static class ValidatedContractReader
{
    public static TemplateDocument ReadTemplate(string json) =>
        EnsureValid(ContractJson.DeserializeTemplate(json), TemplateValidator.Validate, "template");

    public static ProjectIdentityDocument ReadProjectIdentity(string json) =>
        EnsureValid(ContractJson.DeserializeProjectIdentity(json), ProjectIdentityValidator.Validate, "project identity");

    public static ProjectStructureDocument ReadProjectStructure(string json) =>
        EnsureValid(ContractJson.DeserializeProjectStructure(json), ProjectStructureValidator.Validate, "project structure");

    public static TemplateLibraryDocument ReadTemplateLibrary(string json) =>
        EnsureValid(ContractJson.DeserializeTemplateLibrary(json), TemplateLibraryValidator.Validate, "template library");

    private static T EnsureValid<T>(T document, Func<T, ContractValidationResult> validate, string label)
    {
        ContractValidationResult result = validate(document);
        if (!result.IsValid)
        {
            throw new ContractValidationException($"The {label} document violates its supported contract.", result.Issues);
        }

        return document;
    }
}
