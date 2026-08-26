namespace ProjectScaffolder.Core.Generation;

public enum FileSystemEntryKind
{
    None,
    File,
    Directory,
    ReparsePoint,
}

public interface IFileSystemProbe
{
    FileSystemEntryKind GetEntryKind(string path);
}

public sealed class PhysicalFileSystemProbe : IFileSystemProbe
{
    public FileSystemEntryKind GetEntryKind(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return FileSystemEntryKind.None;
        }

        FileAttributes attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return FileSystemEntryKind.ReparsePoint;
        }

        return attributes.HasFlag(FileAttributes.Directory)
            ? FileSystemEntryKind.Directory
            : FileSystemEntryKind.File;
    }
}
