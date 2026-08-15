using System;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Models
{
    public sealed class PreparedRestoreContent
    {
        public PreparedRestoreContent(
            FamilyIdentity familyIdentity,
            VersionId sourceVersionId,
            VersionId expectedCurrentVersionId,
            string preparedFamilyFilePath,
            FamilySnapshot sourceSnapshot,
            string binaryChecksum)
        {
            FamilyIdentity = familyIdentity ?? throw new ArgumentNullException(nameof(familyIdentity));
            SourceVersionId = sourceVersionId ?? throw new ArgumentNullException(nameof(sourceVersionId));
            ExpectedCurrentVersionId = expectedCurrentVersionId ?? throw new ArgumentNullException(nameof(expectedCurrentVersionId));
            PreparedFamilyFilePath = string.IsNullOrWhiteSpace(preparedFamilyFilePath)
                ? throw new ArgumentException("Prepared family file path is required.", nameof(preparedFamilyFilePath))
                : preparedFamilyFilePath;
            SourceSnapshot = sourceSnapshot;
            BinaryChecksum = string.IsNullOrWhiteSpace(binaryChecksum)
                ? throw new ArgumentException("Binary checksum is required.", nameof(binaryChecksum))
                : binaryChecksum;
        }

        public FamilyIdentity FamilyIdentity { get; }
        public VersionId SourceVersionId { get; }
        public VersionId ExpectedCurrentVersionId { get; }
        public string PreparedFamilyFilePath { get; }
        public FamilySnapshot SourceSnapshot { get; }
        public string BinaryChecksum { get; }
    }
}
