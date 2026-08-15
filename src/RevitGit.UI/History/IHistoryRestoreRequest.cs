using RevitGit.Domain.Identifiers;

namespace RevitGit.UI.History
{
    public interface IHistoryRestoreRequest
    {
        void RequestRestore(VersionId sourceVersionId);
    }
}
