using ProjectScaffolder.Core.Persistence;

namespace ProjectScaffolder.Core.Tests;

public sealed class AtomicJsonFileTests
{
    [Fact]
    public async Task ReplacesExistingFileAndKeepsOneBackup()
    {
        string testDirectory = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(testDirectory, "template.json");

        try
        {
            await AtomicJsonFile.SaveAsync(path, "{\"version\":1}\n");
            await AtomicJsonFile.SaveAsync(path, "{\"version\":2}\n");

            Assert.Equal("{\"version\":2}\n", await File.ReadAllTextAsync(path));
            Assert.Equal("{\"version\":1}\n", await File.ReadAllTextAsync(path + ".bak"));
            Assert.Empty(Directory.GetFiles(testDirectory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }
}
