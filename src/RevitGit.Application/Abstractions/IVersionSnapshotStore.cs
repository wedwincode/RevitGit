using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Abstractions
{
    public interface IVersionSnapshotStore
    {
        FamilySnapshot ReadSnapshot(FamilyIdentity familyIdentity, VersionId versionId);
    }
}
