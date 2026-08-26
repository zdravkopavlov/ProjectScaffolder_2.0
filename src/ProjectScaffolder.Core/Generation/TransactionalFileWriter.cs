using System.Security.Cryptography;

namespace ProjectScaffolder.Core.Generation;

internal static class TransactionalFileWriter
{
    public static async Task WriteBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> bytes,
        PreflightDisposition disposition,
        OperationJournal journal,
        CancellationToken cancellationToken)
    {
        string temporaryPath = CreateSiblingPath(destinationPath, "tmp");
        try
        {
            await using (FileStream stream = CreateTemporaryStream(temporaryPath))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            string digest = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
            CommitTemporaryFile(temporaryPath, destinationPath, disposition, digest, journal);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
        }
    }

    public static async Task CopyAssetAsync(
        string sourcePath,
        string destinationPath,
        string expectedSha256,
        PreflightDisposition disposition,
        OperationJournal journal,
        CancellationToken cancellationToken)
    {
        string temporaryPath = CreateSiblingPath(destinationPath, "tmp");
        try
        {
            string digest;
            using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using (FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (FileStream destination = CreateTemporaryStream(temporaryPath))
                {
                    byte[] buffer = new byte[131072];
                    int bytesRead;
                    while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        hash.AppendData(buffer, 0, bytesRead);
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    }

                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                    destination.Flush(true);
                }

                digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            if (!digest.Equals(expectedSha256, StringComparison.Ordinal))
            {
                throw new IOException("The attached asset changed after template validation; its SHA-256 digest no longer matches.");
            }

            CommitTemporaryFile(temporaryPath, destinationPath, disposition, digest, journal);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
        }
    }

    private static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        PreflightDisposition disposition,
        string digest,
        OperationJournal journal)
    {
        switch (disposition)
        {
            case PreflightDisposition.Create:
                File.Move(temporaryPath, destinationPath);
                journal.RecordCreatedFile(destinationPath, digest);
                break;
            case PreflightDisposition.OverwriteExistingFile:
                string backupPath = CreateSiblingPath(destinationPath, "bak");
                File.Replace(temporaryPath, destinationPath, backupPath, true);
                journal.RecordOverwrittenFile(destinationPath, backupPath, digest);
                break;
            default:
                throw new InvalidOperationException($"Disposition '{disposition}' cannot write a file.");
        }
    }

    private static FileStream CreateTemporaryStream(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        131072,
        FileOptions.Asynchronous | FileOptions.WriteThrough);

    private static string CreateSiblingPath(string destinationPath, string extension)
    {
        string? directory = Path.GetDirectoryName(destinationPath);
        string name = Path.GetFileName(destinationPath);
        return Path.Combine(directory ?? throw new InvalidOperationException("The destination has no parent directory."), $".{name}.project-scaffolder-{Guid.NewGuid():N}.{extension}");
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
