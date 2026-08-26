using System.Security.Cryptography;

namespace ProjectScaffolder.Core.Generation;

internal sealed class OperationJournal
{
    private readonly List<JournalEntry> _entries = [];

    public void RecordCreatedDirectory(string path) => _entries.Add(new CreatedDirectoryEntry(path));

    public void RecordCreatedFile(string path, string writtenSha256) =>
        _entries.Add(new CreatedFileEntry(path, writtenSha256));

    public void RecordOverwrittenFile(string path, string backupPath, string writtenSha256) =>
        _entries.Add(new OverwrittenFileEntry(path, backupPath, writtenSha256));

    public void Commit()
    {
        foreach (OverwrittenFileEntry entry in _entries.OfType<OverwrittenFileEntry>())
        {
            try
            {
                if (File.Exists(entry.BackupPath))
                {
                    File.Delete(entry.BackupPath);
                }
            }
            catch (IOException)
            {
                // Backup cleanup is best-effort after the generation transaction has succeeded.
            }
            catch (UnauthorizedAccessException)
            {
                // Backup cleanup is best-effort after the generation transaction has succeeded.
            }
        }

        _entries.Clear();
    }

    public RollbackResult Rollback()
    {
        List<string> pathsRequiringInspection = [];
        foreach (JournalEntry entry in _entries.AsEnumerable().Reverse())
        {
            try
            {
                switch (entry)
                {
                    case CreatedFileEntry createdFile:
                        RollbackCreatedFile(createdFile, pathsRequiringInspection);
                        break;
                    case OverwrittenFileEntry overwrittenFile:
                        RollbackOverwrittenFile(overwrittenFile, pathsRequiringInspection);
                        break;
                    case CreatedDirectoryEntry createdDirectory:
                        RollbackCreatedDirectory(createdDirectory, pathsRequiringInspection);
                        break;
                }
            }
            catch (Exception) when (entry.Path is not null)
            {
                pathsRequiringInspection.Add(entry.Path);
            }
        }

        _entries.Clear();
        return new RollbackResult(pathsRequiringInspection.Count == 0, pathsRequiringInspection.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void RollbackCreatedFile(CreatedFileEntry entry, List<string> incomplete)
    {
        if (!File.Exists(entry.Path))
        {
            return;
        }

        if (!HashMatches(entry.Path, entry.WrittenSha256))
        {
            incomplete.Add(entry.Path);
            return;
        }

        File.SetAttributes(entry.Path, FileAttributes.Normal);
        File.Delete(entry.Path);
    }

    private static void RollbackOverwrittenFile(OverwrittenFileEntry entry, List<string> incomplete)
    {
        if (!File.Exists(entry.BackupPath))
        {
            incomplete.Add(entry.Path);
            return;
        }

        if (File.Exists(entry.Path) && !HashMatches(entry.Path, entry.WrittenSha256))
        {
            incomplete.Add(entry.Path);
            return;
        }

        if (File.Exists(entry.Path))
        {
            File.Replace(entry.BackupPath, entry.Path, null, true);
        }
        else
        {
            File.Move(entry.BackupPath, entry.Path);
        }
    }

    private static void RollbackCreatedDirectory(CreatedDirectoryEntry entry, List<string> incomplete)
    {
        if (!Directory.Exists(entry.Path))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(entry.Path).Any())
        {
            incomplete.Add(entry.Path);
            return;
        }

        Directory.Delete(entry.Path);
    }

    private static bool HashMatches(string path, string expectedSha256)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return actual.Equals(expectedSha256, StringComparison.Ordinal);
    }

    private abstract record JournalEntry(string Path);

    private sealed record CreatedDirectoryEntry(string Path) : JournalEntry(Path);

    private sealed record CreatedFileEntry(string Path, string WrittenSha256) : JournalEntry(Path);

    private sealed record OverwrittenFileEntry(string Path, string BackupPath, string WrittenSha256) : JournalEntry(Path);
}
