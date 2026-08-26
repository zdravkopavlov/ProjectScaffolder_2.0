using System.Text.Json;
using ProjectScaffolder.Core.Persistence;

namespace ProjectScaffolder.Runner;

internal static class DiagnosticLogWriter
{
    private const int MaximumLogFiles = 100;
    private static readonly TimeSpan MaximumLogAge = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<string> WriteAsync(
        string logsRoot,
        RunLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(logsRoot);
        string fileName = $"{entry.TimestampUtc.UtcDateTime:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
        string path = Path.Combine(logsRoot, fileName);
        string json = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
        await AtomicJsonFile.SaveAsync(path, json, false, cancellationToken).ConfigureAwait(false);
        Prune(logsRoot, entry.TimestampUtc.UtcDateTime);
        return path;
    }

    private static void Prune(string logsRoot, DateTime nowUtc)
    {
        try
        {
            FileInfo[] logs = new DirectoryInfo(logsRoot)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToArray();
            IEnumerable<FileInfo> expired = logs
                .Skip(MaximumLogFiles)
                .Concat(logs.Where(file => nowUtc - file.LastWriteTimeUtc > MaximumLogAge))
                .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo log in expired)
            {
                log.Delete();
            }
        }
        catch (IOException)
        {
            // Retention is best-effort and must not turn a successful generation into a failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Retention is best-effort and must not turn a successful generation into a failure.
        }
    }
}
