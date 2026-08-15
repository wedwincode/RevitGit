using System;

namespace RevitGit.Infrastructure.FileSystem.Repositories
{
    public sealed class FamilyRepositoryMetadata
    {
        internal FamilyRepositoryMetadata(
            int storageFormatVersion,
            Guid repositoryId,
            string familyFileName,
            DateTimeOffset createdAt)
        {
            StorageFormatVersion = storageFormatVersion;
            RepositoryId = repositoryId;
            FamilyFileName = familyFileName;
            CreatedAt = createdAt;
        }

        public int StorageFormatVersion { get; }
        public Guid RepositoryId { get; }
        public string FamilyFileName { get; }
        public DateTimeOffset CreatedAt { get; }
    }
}
