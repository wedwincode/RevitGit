using System;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class HistoryPaneCoordinator : IDisposable
    {
        private readonly UIControlledApplication _application;
        private readonly HistoryViewModel _viewModel;
        private readonly RevitExternalEventDispatcher _dispatcher;

        public HistoryPaneCoordinator(UIControlledApplication application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            var bridge = new HistoryRefreshRequestBridge();
            _viewModel = new HistoryViewModel(bridge);
            var handler = new RefreshHistoryExternalEventHandler(_viewModel);
            _dispatcher = new RevitExternalEventDispatcher(handler);
            bridge.Attach(_dispatcher);

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

        public void Dispose()
        {
            _application.ViewActivated -= OnViewActivated;
            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            _dispatcher.Dispose();
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs args) => RequestRefresh();
        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs args) => RequestRefresh();
        private void OnDocumentClosed(object sender, DocumentClosedEventArgs args) => RequestRefresh();

        private void RequestRefresh()
        {
            _viewModel.BeginRefresh();
            _dispatcher.RequestRefresh();
        }
    }
}
