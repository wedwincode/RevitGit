using System;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class HistoryRefreshRequestBridge : IHistoryRefreshRequest, IHistoryCompareRequest
    {
        private IHistoryRefreshRequest _target;
        private IHistoryCompareRequest _compareTarget;

        public void Attach(IHistoryRefreshRequest target)
        {
            if (_target != null)
            {
                throw new InvalidOperationException("History refresh bridge is already attached.");
            }

            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void AttachCompare(IHistoryCompareRequest target)
        {
            _compareTarget = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void RequestRefresh()
        {
            _target?.RequestRefresh();
        }
        public void RequestCompareWithCurrent(Domain.Identifiers.VersionId sourceVersionId) => _compareTarget?.RequestCompareWithCurrent(sourceVersionId);
        public void RequestCompareSaved(Domain.Identifiers.VersionId sourceVersionId, Domain.Identifiers.VersionId targetVersionId) => _compareTarget?.RequestCompareSaved(sourceVersionId, targetVersionId);
    }
}
