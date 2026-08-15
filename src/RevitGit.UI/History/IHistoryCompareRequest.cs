using RevitGit.Domain.Identifiers;

namespace RevitGit.UI.History
{
    public interface IHistoryCompareRequest
    {
        void RequestCompareWithCurrent(VersionId sourceVersionId);
        void RequestCompareSaved(VersionId sourceVersionId, VersionId targetVersionId);
    }
}
