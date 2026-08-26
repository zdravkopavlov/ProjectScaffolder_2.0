using System.Text;

namespace ProjectScaffolder.Core.Persistence;

public static class AtomicJsonFile
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);

    public static async Task SaveAsync(string path, string json, bool keepBackup = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(json);

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is null)
        {
            throw new ArgumentException("The destination must have a parent directory.", nameof(path));
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        string backupPath = fullPath + ".bak";

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (StreamWriter writer = new(stream, Utf8WithoutBom))
            {
                await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, keepBackup ? backupPath : null, true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
