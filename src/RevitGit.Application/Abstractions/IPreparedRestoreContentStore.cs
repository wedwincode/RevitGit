using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Abstractions
{
    public interface IPreparedRestoreContentStore
    {
        PreparedRestoreContent PrepareRestoreContent(
            FamilyIdentity familyIdentity,
            VersionId sourceVersionId,
            VersionId expectedCurrentVersionId);

        void PublishPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared);

        void RollbackPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared);

        void StorePreparedRestore(
            FamilyIdentity familyIdentity,
            PreparedRestoreContent prepared,
            VersionId restoredVersionId);

        void CleanupPreparedRestore(PreparedRestoreContent prepared);
    }
}
