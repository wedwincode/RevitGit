using System;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class HistoryRefreshRequestBridge : IHistoryRefreshRequest
    {
        private IHistoryRefreshRequest _target;

        public void Attach(IHistoryRefreshRequest target)
        {
            if (_target != null)
            {
                throw new InvalidOperationException("History refresh bridge is already attached.");
            }

            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void RequestRefresh()
        {
            _target?.RequestRefresh();
        }
    }
}
