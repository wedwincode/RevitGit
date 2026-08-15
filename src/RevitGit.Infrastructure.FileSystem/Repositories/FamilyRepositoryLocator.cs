using System;
using System.IO;

namespace RevitGit.Infrastructure.FileSystem.Repositories
{
    public sealed class FamilyRepositoryLocator
    {
        public FamilyRepositoryPaths Locate(string familyFilePath, Guid repositoryId)
        {
            if (repositoryId == Guid.Empty)
            {
                throw new ArgumentException("Repository identifier cannot be empty.", nameof(repositoryId));
            }

            var normalizedFamilyPath = NormalizeFamilyPath(familyFilePath);
            var familyDirectory = Path.GetDirectoryName(normalizedFamilyPath);
            var historyRoot = Path.Combine(familyDirectory, ".familyhistory");
            var repositoryDirectory = Path.Combine(
                historyRoot,
                "repositories",
                repositoryId.ToString("N"));

            return new FamilyRepositoryPaths(
                normalizedFamilyPath,
                historyRoot,
                Path.Combine(historyRoot, "index.json"),
                repositoryDirectory,
                Path.Combine(repositoryDirectory, "repository.json"),
                Path.Combine(repositoryDirectory, "history.json"),
                Path.Combine(repositoryDirectory, "versions"));
        }

        public string NormalizeFamilyPath(string familyFilePath)
        {
            if (string.IsNullOrWhiteSpace(familyFilePath))
            {
                throw new ArgumentException("Family file path cannot be empty.", nameof(familyFilePath));
            }

            if (!Path.IsPathRooted(familyFilePath))
            {
                throw new ArgumentException("Family file path must be absolute.", nameof(familyFilePath));
            }

            string normalized;
            try
            {
                normalized = Path.GetFullPath(familyFilePath);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                throw new ArgumentException("Family file path is invalid.", nameof(familyFilePath), exception);
            }

            if (!string.Equals(Path.GetExtension(normalized), ".rfa", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Family file path must have the .rfa extension.", nameof(familyFilePath));
            }

            if (Directory.Exists(normalized))
            {
                throw new ArgumentException("Family file path cannot refer to a directory.", nameof(familyFilePath));
            }

            return normalized;
        }
    }
}
