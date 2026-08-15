using System;
using Autodesk.Revit.UI;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class RevitExternalEventDispatcher : IHistoryRefreshRequest, IDisposable
    {
        private readonly ExternalEvent _externalEvent;

        public RevitExternalEventDispatcher(IExternalEventHandler handler)
        {
            _externalEvent = ExternalEvent.Create(handler ?? throw new ArgumentNullException(nameof(handler)));
        }

        public void RequestRefresh()
        {
            _externalEvent.Raise();
        }

        public void Dispose()
        {
            _externalEvent.Dispose();
        }
    }
}
