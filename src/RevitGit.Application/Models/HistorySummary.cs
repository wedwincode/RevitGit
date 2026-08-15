using System.Collections.Generic;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Models
{
    public sealed class HistorySummary
    {
        public HistorySummary(
            VariantId currentVariantId,
            IReadOnlyList<VariantSummary> variants,
            IReadOnlyList<VersionSummary> versions)
        {
            CurrentVariantId = currentVariantId;
            Variants = variants;
            Versions = versions;
        }

        public VariantId CurrentVariantId { get; }
        public IReadOnlyList<VariantSummary> Variants { get; }
        public IReadOnlyList<VersionSummary> Versions { get; }
    }
}
