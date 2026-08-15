using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Abstractions
{
    public interface IVersionContentStore
    {
        void StoreCurrentVersion(FamilyIdentity familyIdentity, VersionId versionId);

        void RestoreVersionContent(FamilyIdentity familyIdentity, VersionId versionId);
    }
}
