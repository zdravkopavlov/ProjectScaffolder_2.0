using ProjectScaffolder.Core.Generation;

// Deliberately exercise arbitrary wrappers and the framework's default Exception message.
#pragma warning disable CA2201

namespace ProjectScaffolder.Runner.Tests;

public sealed class FailurePresentationTests
{
    private const string Summary = "Project generation failed while applying the validated plan.";
    private const string Denied = @"Access to the path 'E:\PHOTOGRAMMETRY\Stone_mushroom_kardzhali\.projectCatalog' is denied.";

    [Fact]
    public void WrappedAccessDeniedPreservesDialogContext()
    {
        var exception = new ProjectGenerationException(Summary, innerException: new UnauthorizedAccessException(Denied));
        string? reason = FailureReason.Select(exception, Summary);
        Assert.Equal(Denied, reason);
        Guid id = Guid.CreateVersion7();
        string text = FailureDialog.BuildMessage(id, "target", Summary, true, null, "log.json", reason);
        Assert.StartsWith(Summary + Environment.NewLine, text, StringComparison.Ordinal);
        Assert.Contains("Reason: " + Denied, text, StringComparison.Ordinal);
        Assert.Contains("Template: " + id, text, StringComparison.Ordinal);
        Assert.Contains("Target: target", text, StringComparison.Ordinal);
        Assert.Contains("Rollback: Completed", text, StringComparison.Ordinal);
        Assert.Contains("Diagnostic log: log.json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UnauthorizedAccessException", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectsDeepestUsefulMessageAcrossDifferentExceptionTypes()
    {
        var exception = new ProjectGenerationException(Summary, innerException:
            new InvalidOperationException("Could not copy asset.", new IOException("The disk is full.")));
        Assert.Equal("The disk is full.", FailureReason.Select(exception, Summary));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Failure\r\n   at Example.Method()")]
    [InlineData("System.Exception: failure ---> System.Exception: nested")]
    public void UnusableInnerMessageDoesNotAddReason(string message)
    {
        Assert.Null(FailureReason.Select(new Exception(Summary, new Exception(message)), Summary));
    }

    [Fact]
    public void NoInnerOrDuplicateOrDefaultOrLongMessageDoesNotAddReason()
    {
        Assert.Null(FailureReason.Select(new ProjectGenerationException(Summary), Summary));
        Assert.Null(FailureReason.Select(new Exception(Summary, new Exception(Summary)), Summary));
        Assert.Null(FailureReason.Select(new Exception(Summary, new Exception()), Summary));
        Assert.Null(FailureReason.Select(new Exception(Summary, new Exception(new string('x', 501))), Summary));
    }

    [Fact]
    public void FallsBackToNearestUsefulMessageAndHandlesAggregates()
    {
        var useful = new IOException("The disk is full.", new Exception(""));
        Assert.Equal(useful.Message, FailureReason.Select(new Exception(Summary, useful), Summary));
        Assert.Equal(useful.Message, FailureReason.Select(new AggregateException(useful), Summary));
        Assert.Null(FailureReason.Select(new Exception(Summary,
            new AggregateException(useful, new IOException("Other failure."))), Summary));
    }

    [Fact]
    public void IncompleteRollbackAndMissingLogRemainVisibleWithoutReason()
    {
        string text = FailureDialog.BuildMessage(null, null, Summary, false, ["inspect-me"], null);
        Assert.DoesNotContain("Reason:", text, StringComparison.Ordinal);
        Assert.Contains("Template: Unknown", text, StringComparison.Ordinal);
        Assert.Contains("Target: Unknown", text, StringComparison.Ordinal);
        Assert.Contains("Rollback: Incomplete", text, StringComparison.Ordinal);
        Assert.Contains("Needs inspection: inspect-me", text, StringComparison.Ordinal);
        Assert.Contains("Diagnostic log: Could not be written", text, StringComparison.Ordinal);
    }
}
