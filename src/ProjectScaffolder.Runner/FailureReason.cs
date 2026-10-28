namespace ProjectScaffolder.Runner;

internal static class FailureReason
{
    internal static string? Select(Exception? exception, string summary)
    {
        string? reason = null;
        // Bound traversal and presentation. Never render ToString() or enumerate branches.
        for (int depth = 0; exception is not null && depth < 32; depth++)
        {
            if (exception is AggregateException aggregate)
            {
                if (aggregate.InnerExceptions.Count != 1)
                {
                    break;
                }

                exception = aggregate.InnerExceptions[0];
                continue;
            }

            string message = exception.Message.Trim();
            if (message.Length is > 0 and <= 500
                && !message.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029')
                && !message.StartsWith("Exception of type '", StringComparison.Ordinal)
                && !message.StartsWith("at ", StringComparison.Ordinal)
                && !message.Contains("--->", StringComparison.Ordinal))
            {
                reason = message;
            }

            exception = exception.InnerException;
        }

        return string.Equals(reason, summary.Trim(), StringComparison.Ordinal) ? null : reason;
    }
}
