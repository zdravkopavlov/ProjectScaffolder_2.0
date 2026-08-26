using System.Security.Cryptography;
using ProjectScaffolder.Core.Contracts;
using ProjectScaffolder.Core.Validation;

namespace ProjectScaffolder.Core.Tests;

public sealed class TemplateAssetValidatorTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptsContainedAssetWithMatchingDigest()
    {
        string templateDirectory = CreateTestDirectory();
        try
        {
            byte[] content = [1, 2, 3, 4];
            string assetPath = Path.Combine(templateDirectory, "assets", "starter.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            File.WriteAllBytes(assetPath, content);
            TemplateDocument template = CreateTemplate(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

            ContractValidationResult result = TemplateAssetValidator.Validate(template, templateDirectory);

            Assert.True(result.IsValid);
        }
        finally
        {
            Directory.Delete(templateDirectory, true);
        }
    }

    [Fact]
    public void RejectsDigestMismatch()
    {
        string templateDirectory = CreateTestDirectory();
        try
        {
            string assetPath = Path.Combine(templateDirectory, "assets", "starter.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            File.WriteAllBytes(assetPath, [1, 2, 3, 4]);
            TemplateDocument template = CreateTemplate(new string('0', 64));

            ContractValidationResult result = TemplateAssetValidator.Validate(template, templateDirectory);

            Assert.Contains(result.Issues, issue => issue.Code == "asset.hash");
        }
        finally
        {
            Directory.Delete(templateDirectory, true);
        }
    }

    private static string CreateTestDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "ProjectScaffolder.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static TemplateDocument CreateTemplate(string digest) => new()
    {
        SchemaVersion = 1,
        TemplateId = Guid.CreateVersion7(Timestamp),
        Name = "Asset Test",
        TemplateVersion = 1,
        CreatedAtUtc = Timestamp,
        ModifiedAtUtc = Timestamp,
        DefaultFileCollision = FileCollisionPolicy.Skip,
        Nodes = [
            new AssetFileNode
            {
                NodeId = Guid.CreateVersion7(Timestamp.AddMilliseconds(1)),
                Name = "starter.bin",
                Asset = new AssetReference
                {
                    Path = "assets/starter.bin",
                    Sha256 = digest,
                },
            },
        ],
    };
}
