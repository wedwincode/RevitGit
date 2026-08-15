using RevitGit.Application.Models;
using RevitGit.Domain.History;

namespace RevitGit.Application.Abstractions
{
    public interface IHistoryRepository
    {
        bool Exists(FamilyIdentity familyIdentity);

        FamilyHistory Load(FamilyIdentity familyIdentity);

        void Save(FamilyIdentity familyIdentity, FamilyHistory history);
    }
}
