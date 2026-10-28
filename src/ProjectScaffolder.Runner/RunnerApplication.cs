using System.Reflection;
using System.Text.Json;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Generation;
using ProjectScaffolder.Core.Serialization;

namespace ProjectScaffolder.Runner;

internal static class RunnerApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        RunnerOptions? options = null;
        TemplateDocument? template = null;
        string applicationVersion = GetApplicationVersion();

        try
        {
            options = RunnerOptionsParser.Parse(args);
            string templateDirectory = Path.Combine(options.TemplatesRoot, options.TemplateId.ToString("D"));
            string templatePath = Path.Combine(templateDirectory, "template.json");
            if (!File.Exists(templatePath))
            {
                throw new RunnerFailureException(RunnerExitCode.TemplateNotFound, "The selected template could not be found.");
            }

            try
            {
                template = ValidatedContractReader.ReadTemplate(await File.ReadAllTextAsync(templatePath).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is JsonException or ContractValidationException or IOException or UnauthorizedAccessException)
            {
                throw new RunnerFailureException(RunnerExitCode.InvalidTemplate, "The selected template is unreadable or invalid.", exception);
            }

            if (template.TemplateId != options.TemplateId)
            {
                throw new RunnerFailureException(RunnerExitCode.InvalidTemplate, "The selected template ID does not match its template document.");
            }

            GenerationResult result = await ProjectGenerator.GenerateAsync(new GenerationRequest
            {
                Template = template,
                TemplateDirectory = templateDirectory,
                TargetDirectory = options.TargetPath,
                GeneratorVersion = applicationVersion,
            }).ConfigureAwait(false);

            await TryWriteLogAsync(options.LogsRoot, new RunLogEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ApplicationVersion = applicationVersion,
                TemplateId = template.TemplateId,
                TemplateVersion = template.TemplateVersion,
                TargetPath = options.TargetPath,
                Outcome = "success",
                CreatedDirectories = result.CreatedDirectoryCount,
                ReusedDirectories = result.ReusedDirectoryCount,
                CreatedFiles = result.CreatedFileCount,
                SkippedFiles = result.SkippedFileCount,
                OverwrittenFiles = result.OverwrittenFileCount,
            }).ConfigureAwait(false);
            return (int)RunnerExitCode.Success;
        }
        catch (RunnerOptionsException exception)
        {
            return await HandleFailureAsync(
                RunnerExitCode.InvalidArguments,
                options,
                template,
                applicationVersion,
                exception.Message,
                exception,
                null,
                args.Contains("--no-ui", StringComparer.Ordinal)).ConfigureAwait(false);
        }
        catch (RunnerFailureException exception)
        {
            return await HandleFailureAsync(
                exception.ExitCode,
                options,
                template,
                applicationVersion,
                exception.Message,
                exception.InnerException ?? exception,
                null,
                options?.SuppressUi ?? false).ConfigureAwait(false);
        }
        catch (ProjectGenerationException exception)
        {
            RunnerExitCode exitCode = GetGenerationExitCode(exception);
            string message = exception.Issues.Count > 0 ? exception.Issues[0].Message : exception.Message;
            return await HandleFailureAsync(
                exitCode,
                options,
                template,
                applicationVersion,
                message,
                exception,
                exception.Rollback,
                options?.SuppressUi ?? false).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return await HandleFailureAsync(
                RunnerExitCode.UnexpectedFailure,
                options,
                template,
                applicationVersion,
                "An unexpected error prevented project generation.",
                exception,
                null,
                options?.SuppressUi ?? false).ConfigureAwait(false);
        }
    }

    private static async Task<int> HandleFailureAsync(
        RunnerExitCode exitCode,
        RunnerOptions? options,
        TemplateDocument? template,
        string applicationVersion,
        string message,
        Exception exception,
        RollbackResult? rollback,
        bool suppressUi)
    {
        string logsRoot = options?.LogsRoot ?? GetDefaultLogsRoot();
        string? logPath = await TryWriteLogAsync(logsRoot, new RunLogEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ApplicationVersion = applicationVersion,
            TemplateId = options?.TemplateId,
            TemplateVersion = template?.TemplateVersion,
            TargetPath = options?.TargetPath,
            Outcome = "failure",
            RollbackCompleted = rollback?.Completed,
            PathsRequiringInspection = rollback?.PathsRequiringInspection,
            ErrorType = exception.GetType().FullName,
            ErrorMessage = exception.Message,
            ErrorDetails = exception.ToString(),
        }).ConfigureAwait(false);

        if (!suppressUi)
        {
            FailureDialog.Show(
                options?.TemplateId,
                options?.TargetPath,
                message,
                rollback?.Completed,
                rollback?.PathsRequiringInspection,
                logPath,
                FailureReason.Select(exception, message));
        }

        return (int)exitCode;
    }

    private static async Task<string?> TryWriteLogAsync(string logsRoot, RunLogEntry entry)
    {
        try
        {
            return await DiagnosticLogWriter.WriteAsync(logsRoot, entry).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static RunnerExitCode GetGenerationExitCode(ProjectGenerationException exception)
    {
        if (exception.Rollback is { Completed: false })
        {
            return RunnerExitCode.RollbackIncomplete;
        }

        if (exception.Issues.Any(issue => issue.Code.StartsWith("target.", StringComparison.Ordinal)))
        {
            return RunnerExitCode.TargetConflict;
        }

        if (exception.Issues.Count > 0)
        {
            return RunnerExitCode.InvalidTemplate;
        }

        return RunnerExitCode.GenerationFailed;
    }

    private static string GetApplicationVersion()
    {
        string? version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return version?.Split('+', 2)[0] ?? "2.0.0-alpha.7";
    }

    private static string GetDefaultLogsRoot()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localData, "ProjectScaffolder", "Logs");
    }
}

internal sealed class RunnerFailureException(
    RunnerExitCode exitCode,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public RunnerExitCode ExitCode { get; } = exitCode;
}
