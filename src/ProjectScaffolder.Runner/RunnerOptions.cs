using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Runner;

internal sealed record RunnerOptions
{
    public required Guid TemplateId { get; init; }

    public required string TargetPath { get; init; }

    public required string TemplatesRoot { get; init; }

    public required string LogsRoot { get; init; }

    public required bool SuppressUi { get; init; }
}

internal static class RunnerOptionsParser
{
    public static RunnerOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        bool suppressUi = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument.Equals("--no-ui", StringComparison.Ordinal))
            {
                if (suppressUi)
                {
                    throw new RunnerOptionsException("The --no-ui option was specified more than once.");
                }

                suppressUi = true;
                continue;
            }

            if (argument is not ("--template-id" or "--target" or "--templates-root" or "--logs-root"))
            {
                throw new RunnerOptionsException($"Unknown runner option '{argument}'.");
            }

            if (index + 1 >= args.Length)
            {
                throw new RunnerOptionsException($"Runner option '{argument}' requires a value.");
            }

            if (!values.TryAdd(argument, args[++index]))
            {
                throw new RunnerOptionsException($"Runner option '{argument}' was specified more than once.");
            }
        }

        if (!values.TryGetValue("--template-id", out string? templateIdText) ||
            !Guid.TryParseExact(templateIdText, "D", out Guid templateId) ||
            !UuidRules.IsVersion7(templateId))
        {
            throw new RunnerOptionsException("--template-id must contain a canonical UUIDv7 value.");
        }

        if (!values.TryGetValue("--target", out string? targetPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            throw new RunnerOptionsException("--target must contain the existing project folder path.");
        }

        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string applicationData = Path.Combine(localData, "ProjectScaffolder");
        string templatesRoot = values.GetValueOrDefault("--templates-root") ?? Path.Combine(applicationData, "Templates");
        string logsRoot = values.GetValueOrDefault("--logs-root") ?? Path.Combine(applicationData, "Logs");

        return new RunnerOptions
        {
            TemplateId = templateId,
            TargetPath = Path.GetFullPath(targetPath),
            TemplatesRoot = Path.GetFullPath(templatesRoot),
            LogsRoot = Path.GetFullPath(logsRoot),
            SuppressUi = suppressUi,
        };
    }
}

internal sealed class RunnerOptionsException(string message) : Exception(message);
