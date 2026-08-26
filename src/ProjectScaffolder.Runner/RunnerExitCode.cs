namespace ProjectScaffolder.Runner;

internal enum RunnerExitCode
{
    Success = 0,
    InvalidArguments = 2,
    TemplateNotFound = 3,
    InvalidTemplate = 4,
    TargetConflict = 5,
    GenerationFailed = 6,
    RollbackIncomplete = 7,
    UnexpectedFailure = 10,
}
