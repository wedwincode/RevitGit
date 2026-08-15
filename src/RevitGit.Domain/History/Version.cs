using System;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Domain.History
{
    public sealed class Version
    {
        internal Version(
            VersionId id,
            VersionId parentVersionId,
            DateTimeOffset createdAt,
            string comment,
            VersionId restoredFromVersionId)
        {
            if (id == null)
            {
                throw new DomainException("Version identifier is required.");
            }

            if (id.Equals(parentVersionId))
            {
                throw new DomainException("A version cannot be its own parent.");
            }

            if (id.Equals(restoredFromVersionId))
            {
                throw new DomainException("A version cannot be restored from itself.");
            }

            Id = id;
            ParentVersionId = parentVersionId;
            CreatedAt = createdAt;
            Comment = NormalizeComment(comment);
            RestoredFromVersionId = restoredFromVersionId;
        }

        public VersionId Id { get; }

        public VersionId ParentVersionId { get; }

        public DateTimeOffset CreatedAt { get; }

        public string Comment { get; }

        public VersionId RestoredFromVersionId { get; }

        private static string NormalizeComment(string comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        }
    }
}
