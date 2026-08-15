using RevitGit.Domain.Identifiers;

namespace RevitGit.Domain.Exceptions
{
    public sealed class UnknownVersionException : DomainException
    {
        public UnknownVersionException(VersionId versionId)
            : base(versionId == null
                ? "Version identifier is required."
                : "Version does not exist: " + versionId + ".")
        {
            VersionId = versionId;
        }

        public VersionId VersionId { get; }
    }
}
