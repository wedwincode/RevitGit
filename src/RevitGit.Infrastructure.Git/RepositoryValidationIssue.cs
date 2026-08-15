using RevitGit.Domain.Identifiers;

namespace RevitGit.Infrastructure.Git
{
    public sealed class RepositoryValidationIssue
    {
        public RepositoryValidationIssue(string code, string message, VersionId versionId = null, VariantId variantId = null)
        {
            Code = code;
            Message = message;
            VersionId = versionId;
            VariantId = variantId;
        }

        public string Code { get; }
        public string Message { get; }
        public VersionId VersionId { get; }
        public VariantId VariantId { get; }
    }
}
