namespace ProjectScaffolder.Core.Variables;

public sealed class VariableResolutionException : Exception
{
    public VariableResolutionException(string message)
        : base(message)
    {
    }
}
