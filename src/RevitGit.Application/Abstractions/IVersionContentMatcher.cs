using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Abstractions
{
    public interface IVersionContentMatcher
    {
        bool WorkingFileMatchesVersion(FamilyIdentity familyIdentity, VersionId versionId);
    }
}
