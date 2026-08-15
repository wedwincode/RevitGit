using RevitGit.Application.Models;

namespace RevitGit.Application.Abstractions
{
    public interface IFamilyDocumentGateway
    {
        FamilyIdentity GetIdentity();

        void Save();
    }
}
