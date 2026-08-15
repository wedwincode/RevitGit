using System;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitGit.UI.History;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Revit2021.History
{
    internal sealed class HistoryPaneCoordinator : IDisposable, IHistoryCompareRequest, IHistoryRestoreRequest
    {
        private readonly UIControlledApplication _application;
        private readonly HistoryViewModel _viewModel;
        private readonly RevitExternalEventDispatcher _dispatcher;
        private bool _isRestoring;

        public HistoryPaneCoordinator(UIControlledApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            var bridge = new HistoryRefreshRequestBridge();
            _viewModel = new HistoryViewModel(bridge, bridge, bridge);
            var handler = new RefreshHistoryExternalEventHandler(_viewModel, RestoreCompleted);
            _dispatcher = new RevitExternalEventDispatcher(handler);
            bridge.Attach(_dispatcher);
            bridge.AttachCompare(this);
            bridge.AttachRestore(this);

            var provider = new HistoryDockablePaneProvider(new HistoryView(_viewModel));
            _application.RegisterDockablePane(HistoryPaneIds.PaneId, "История семейства", provider);
            _application.ViewActivated += OnViewActivated;
            _application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            _application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        }

        public void Show(UIApplication application)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            application.GetDockablePane(HistoryPaneIds.PaneId).Show();
            RequestRefresh();
        }

        public void NotifyHistoryChanged()
        {
            RequestRefresh();
        }

        public void RequestCompareWithCurrent(VersionId sourceVersionId)
        {
            ((RefreshHistoryExternalEventHandler)_dispatcher.Handler).QueueCompareWithCurrent(sourceVersionId);
            _dispatcher.Raise();
        }

        public void RequestCompareSaved(VersionId sourceVersionId, VersionId targetVersionId)
        {
            ((RefreshHistoryExternalEventHandler)_dispatcher.Handler).CompareSaved(sourceVersionId, targetVersionId);
        }

        public void RequestRestore(VersionId sourceVersionId)
        {
            if (_isRestoring) return;
            _isRestoring = true;
            ((RefreshHistoryExternalEventHandler)_dispatcher.Handler).QueueRestore(sourceVersionId);
            _dispatcher.Raise();
        }

        public void Dispose()
        {
            _application.ViewActivated -= OnViewActivated;
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            _dispatcher.Dispose();
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs args) { if (!_isRestoring) RequestRefresh(); }
        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs args) { if (!_isRestoring) RequestRefresh(); }
        private void OnDocumentClosed(object sender, DocumentClosedEventArgs args) { if (!_isRestoring) RequestRefresh(); }

        private void RestoreCompleted()
        {
            _isRestoring = false;
        }

        private void RequestRefresh()
        {
            _viewModel.BeginRefresh();
            _dispatcher.RequestRefresh();
        }
    }
}
