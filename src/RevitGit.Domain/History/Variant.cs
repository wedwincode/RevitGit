using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Domain.History
{
    public sealed class Variant
    {
        internal Variant(VariantId id, string name, VersionId currentVersionId)
        {
            if (id == null)
            {
                throw new DomainException("Variant identifier is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Variant name cannot be empty.");
            }

            if (currentVersionId == null)
            {
                throw new DomainException("Current version identifier is required.");
            }

            Id = id;
            Name = name.Trim();
            CurrentVersionId = currentVersionId;
        }

        public VariantId Id { get; }

        public string Name { get; }

        public VersionId CurrentVersionId { get; private set; }

        public static Variant Rehydrate(VariantId id, string name, VersionId currentVersionId)
        {
            return new Variant(id, name, currentVersionId);
        }

        internal void MoveTo(VersionId versionId)
        {
            if (versionId == null)
            {
                throw new DomainException("Current version identifier is required.");
            }

            CurrentVersionId = versionId;
        }
    }
}
