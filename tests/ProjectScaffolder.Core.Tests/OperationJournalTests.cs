using System.Text;
using ProjectScaffolder.Core.Generation;

namespace ProjectScaffolder.Core.Tests;

public sealed class OperationJournalTests
{
    [Fact]
    public async Task RollbackRestoresOverwrittenFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.JournalTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "artist-file.txt");
        await File.WriteAllTextAsync(path, "original artist content");
        OperationJournal journal = new();

        try
        {
            await TransactionalFileWriter.WriteBytesAsync(
                path,
                Encoding.UTF8.GetBytes("generated replacement"),
                PreflightDisposition.OverwriteExistingFile,
                journal,
                CancellationToken.None);

            Assert.Equal("generated replacement", await File.ReadAllTextAsync(path));
            RollbackResult result = journal.Rollback();

            Assert.True(result.Completed);
            Assert.Equal("original artist content", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task RollbackDoesNotDeleteFileChangedAfterGeneration()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.JournalTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "new-file.txt");
        OperationJournal journal = new();

        try
        {
            await TransactionalFileWriter.WriteBytesAsync(
                path,
                Encoding.UTF8.GetBytes("generated"),
                PreflightDisposition.Create,
                journal,
                CancellationToken.None);
            await File.WriteAllTextAsync(path, "artist changed it");

            RollbackResult result = journal.Rollback();

            Assert.False(result.Completed);
            Assert.Contains(path, result.PathsRequiringInspection, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("artist changed it", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
