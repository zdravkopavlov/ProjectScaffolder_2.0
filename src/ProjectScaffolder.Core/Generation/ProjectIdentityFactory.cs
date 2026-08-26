using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Variables;

namespace ProjectScaffolder.Core.Generation;

internal static class ProjectIdentityFactory
{
    public static ProjectIdentityDocument Create(
        TemplateDocument template,
        GenerationContext context,
        string generatorVersion) => new()
        {
            SchemaVersion = 1,
            ProjectId = context.ProjectId,
            CreatedAtUtc = context.CreatedAtUtc,
            ProjectName = context.ProjectName,
            Template = new ProjectTemplateProvenance
            {
                Id = template.TemplateId,
                Name = template.Name,
                Version = template.TemplateVersion,
            },
            Generator = new GeneratorProvenance
            {
                Name = "Project Scaffolder",
                Version = generatorVersion,
            },
        };
}
