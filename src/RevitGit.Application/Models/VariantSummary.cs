using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Models
{
    public sealed class VariantSummary
    {
        public VariantSummary(
            VariantId id,
            string name,
            VersionId currentVersionId,
            bool isCurrent)
        {
            Id = id;
            Name = name;
            CurrentVersionId = currentVersionId;
            IsCurrent = isCurrent;
        }

        public VariantId Id { get; }
        public string Name { get; }
        public VersionId CurrentVersionId { get; }
        public bool IsCurrent { get; }
    }
}
