using System;
using Autodesk.Revit.UI;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class RevitExternalEventDispatcher : IHistoryRefreshRequest, IDisposable
    {
        private readonly ExternalEvent _externalEvent;
        public IExternalEventHandler Handler { get; }

        public RevitExternalEventDispatcher(IExternalEventHandler handler)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _externalEvent = ExternalEvent.Create(Handler);
        }

        public void RequestRefresh()
        {
            Raise();
        }

        public void Raise() => _externalEvent.Raise();

        public void Dispose()
        {
            _externalEvent.Dispose();
        }
    }
}
