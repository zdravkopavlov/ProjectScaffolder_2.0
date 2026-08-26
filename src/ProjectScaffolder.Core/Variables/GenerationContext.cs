using System.Globalization;

namespace ProjectScaffolder.Core.Variables;

public sealed record GenerationContext
{
    public required string ProjectName { get; init; }

    public required string ProjectPath { get; init; }

    public required Guid ProjectId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required string UserName { get; init; }

    public IReadOnlyDictionary<string, string> ToVariableValues() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["project_name"] = ProjectName,
        ["project_path"] = ProjectPath,
        ["project_id"] = ProjectId.ToString("D"),
        ["created_at_utc"] = CreatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        ["created_date"] = CreatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["user_name"] = UserName,
    };

    public static GenerationContext CreateNew(string targetPath, string? userName = null, DateTimeOffset? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        string canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        DateTimeOffset created = (createdAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();

        return new GenerationContext
        {
            ProjectName = new DirectoryInfo(canonicalPath).Name,
            ProjectPath = canonicalPath,
            ProjectId = Guid.CreateVersion7(created),
            CreatedAtUtc = created,
            UserName = userName ?? Environment.UserName,
        };
    }
}
