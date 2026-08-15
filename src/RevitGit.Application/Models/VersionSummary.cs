using System;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Models
{
    public sealed class VersionSummary
    {
        public VersionSummary(
            VersionId id,
            VersionId parentVersionId,
            DateTimeOffset createdAt,
            string comment,
            VersionId restoredFromVersionId,
            bool isCurrent)
        {
            Id = id;
            ParentVersionId = parentVersionId;
            CreatedAt = createdAt;
            Comment = comment;
            RestoredFromVersionId = restoredFromVersionId;
            IsCurrent = isCurrent;
        }

        public VersionId Id { get; }
        public VersionId ParentVersionId { get; }
        public DateTimeOffset CreatedAt { get; }
        public string Comment { get; }
        public VersionId RestoredFromVersionId { get; }
        public bool IsCurrent { get; }
    }
}
